using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer.ChunkedStream
{
    /// <summary>
    /// This class is a more performant alternative to CTR AES.
    /// 
    /// <para>Wraps a stream with a generic AES encryption algorithm. The decrypted blocks are
    /// cached and mapped back to the original file dynamically, allowing the stream to mimic a
    /// regular memory stream to the recipient object.</para>
    /// </summary>
    internal class AesChunkedStream : Stream
    {
        /*
        Basically, AES CTR works a bit like this, but rotating the salt and using a very small buffer.
        
        This form of encryption serves a good demonstrative example of AES principles, and confers some
        security advantages with respect to the amount of decrypted data kept in memory at any given time.

        However, due to IO latency on Random RW, the dramatically better IO throughput of seq R/W on all storage types, and
        the heavily hardware accelerated nature of AES-NI, it's much faster to use larger blocks.

        Which leaves us here. 
         */ 


        /// <summary>
        /// The desired size of each chunk
        /// </summary>
        static int CHUNK_SIZE = 1024 * 1024;
        private class EChunk
        {
            public byte[]? data;
            /// <summary>
            /// The length of the encrypted block
            /// </summary>
            public long elength;
            /// <summary>
            /// The position of the encrypted block in the file
            /// </summary>
            public long dec_length;

            /// <summary>
            /// Cached value for the position of this block in the decrypted file
            /// </summary>
            public long dec_offset;
            /// <summary>
            /// Cached value for the position of this block in the decrypted file
            /// </summary>
            public long epos;

            public DateTime ticktime;

            public int id;
            static int _id;
            public EChunk(int id, long elength = 0, long dlength = 0, long _eoffset = 0, long _doffset = 0)
            {
                this.id = id;
                this.elength = elength;
                dec_length = dlength;
                dec_offset = _doffset;
                epos = _eoffset;
                ticktime = DateTime.UtcNow;
            }

        }

        /// <summary>
        /// The underlying stream
        /// </summary>
        private Stream IOStream;
        /// <summary>
        /// The key (cached in the stream)
        /// </summary>
        private (byte[] Key, byte[] Iv) cached_key;
        /// <summary>
        /// The chunks representing this stream (note: chunk <see cref="EChunk.data"/> may not be populated)
        /// </summary>
        private EChunk[]? _Chunks;

        /// <summary>
        /// The imaginary position in the decrypted file
        /// </summary>
        private long DecPosition;

        /// <summary>
        /// An internal mapping of the position of the stream
        /// </summary>
        private long EncryptedPosition => IOStream.Position;


        /// <summary>
        /// The imaginary length of the decrypted file
        /// </summary>
        public long DecLength;

        //the stream tracks the average read-rate and predictively adapts the cache-ahead policy
        //currently don't use these fields as the stream appears fast enough
        private int _CacheAhead = 1;
        private double avgRate;
        private DateTime TimeTracker;

        /// <summary>
        /// A progress object that can be used as a callback container
        /// to measure the progress of the file encryption asynchronously
        /// </summary>
        public class ProgressObject
        {
            /// <summary>
            /// A flag indicating whether or not the underlying task has been completed.
            /// This flag will be true when the callback is called.
            /// </summary>
            public bool completed = false;
            /// <summary>
            /// A floating point from 0-1 indicating the approximate progress of the operation.
            /// </summary>
            public double progress = 0d;
            /// <summary>
            /// A callback delegate to be run when the task is completed
            /// </summary>
            public Action? Callback;
            public void AwaitCompletion()
            {
                while (!completed)
                {
                    Thread.Sleep(5);
                }
            }
            /// <summary>
            /// Notify the PO that the task has completed
            /// </summary>
            public void notify()
            {
                completed = true;
                Callback?.Invoke();
            }
        }

        /// <summary>
        /// Encrypts the file given. NOTE: the encryption is delegated to the threadpool and occurs asynchronously.
        /// 
        /// <para><see cref="ProgressObject.completed"/> and <see cref="ProgressObject.Callback"/> allow monitoring of the async task operation.</para>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output"></param>
        /// <param name="key"></param>
        /// <param name="callback">The callback function called when the task completes.</param>
        /// <returns></returns>
        public static ProgressObject EncryptFile(string input, string output, string key, Action? callback)
        {
            ProgressObject po = new ProgressObject();
            if (callback != null) po.Callback = callback;

            Task.Run(() =>
            {
                using (FileStream fi = File.OpenRead(input))
                {
                    using (FileStream fo = File.OpenWrite(output))
                    {
                        var k = AesHelper.DeriveKeyFromPassword(key);

                        //now we need to chunk the file
                        int chunks = (int)double.Ceiling(fi.Length / (double)CHUNK_SIZE);

                        //now we know there are gonna be 4 + (chunks * 8) writes
                        //so we can precalculate the size of the header and handle the offsets preemptively
                        int offset = sizeof(int) + chunks * 2 * sizeof(long);
                        byte[] header = new byte[offset];
                        fo.Write(header, 0, offset);

                        //now we can start chunking the file
                        int pos = 0;
                        byte[] block = new byte[CHUNK_SIZE];
                        double processed = 0;

                        List<(long elength, long dlength)> headerParts = new();
                        for (int i = 0; i < chunks; i++)
                        {
                            //try to read the block length out of the file
                            //first we need to measure the number of bytes
                            int read = (int)long.Min(fi.Length - pos, block.LongLength);
                            if (read != block.Length)
                            {
                                //we need a different size byte buffer, so let's just make one
                                block = new byte[read];
                            }
                            //now read the file into the block
                            fi.Read(block, 0, read);
                            //Encrypt the block and advance
                            var ec = AesHelper.Encrypt(block, k);
                            pos += read;
                            //and write the data into the output filestream
                            fo.Write(ec, 0, ec.Length);

                            //add the header info
                            headerParts.Add((ec.Length, block.Length));
                            //if we have a progress object, notify it of the progress percentage
                            if (po != null) po.progress = (double)++processed / chunks;
                        }

                        //jump to the start of the stream and write the header into it
                        fo.Seek(0, SeekOrigin.Begin);
                        using (BinaryWriter bw = new BinaryWriter(fo))
                        {
                            bw.Write(chunks);
                            foreach (var s in headerParts)
                            {
                                bw.Write(s.elength);
                                bw.Write(s.dlength);
                            }
                        }
                    }
                }
                //and we're done now
                po?.notify();
            });
            //send the po back
            return po;
        }



        /// <summary>
        /// Creates a new chunked AES stream wrapping the provided input stream
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="password">The plaintext password to use</param>
        public AesChunkedStream(Stream inputStream, string password)
        {
            cached_key = AesHelper.DeriveKeyFromPassword(password);
            IOStream = inputStream;
            ReadHeader();
        }

        /// <summary>
        /// Provides a chunked stream to decrypt the given input stream with a plaintext password
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static AesChunkedStream Read(Stream inputStream, string key)
        {
            AesChunkedStream es = new AesChunkedStream(inputStream, key);
            //read the header
            return es;
        }

        /// <summary>
        /// Reads the header from the underlying stream
        /// </summary>
        public void ReadHeader()
        {
            try
            {
                IOStream.Seek(0, SeekOrigin.Begin);
                //read the number of chunks
                BinaryReader br = new BinaryReader(IOStream);
                _Chunks = new EChunk[br.ReadInt32()];
                DecLength = 0;
                //now prepare the EChunks
                long epos = sizeof(int) + 2 * sizeof(long) * _Chunks.Length;
                for (int i = 0; i < _Chunks.Length; i++)
                {
                    _Chunks[i] = new EChunk(
                        id: i,
                        elength: br.ReadInt64(),
                        dlength: br.ReadInt64(),
                        _eoffset: epos,
                        _doffset: DecLength);

                    epos += _Chunks[i].elength;
                    DecLength += _Chunks[i].dec_length;
                }
                //and now set the position of the imaginary stream to 0
                Position = 0;
            }
            catch
            {
                //...
            }

        }

        /// <summary>
        /// Gets the EChunk that contains the byte at the specified index
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private EChunk? GetChunk(long pos)
        {
            if (_Chunks == null) return null;
            //Divide pos by chunk size and floor to get the index
            int index = (int)(pos / CHUNK_SIZE);
            if (index == _Chunks.Length) index -= 1;
            if (index < 0 || index >= _Chunks.Length) return null;

            if (_Chunks != null)
            {
                //Now we need to check that the chunk is all-g
                if (_Chunks[index].data == null)
                {
                    //grab the encrypted block out of the input stream
                    byte[] data = new byte[_Chunks[index].elength];
                    IOStream.Position = _Chunks[index].epos;
                    IOStream.Read(data, 0, data.Length);
                    //Now decrypt it
                    _Chunks[index].data = AesHelper.Decrypt(data, cached_key);

                    //mark when it was cached
                    _Chunks[index].ticktime = DateTime.UtcNow;
                    //clean old chunks
                    foreach (var c in _Chunks)
                    {
                        //
                        if (c.data != null
                            && c != _Chunks[index]
                            //keep a couple of chunks in either direction
                            && (c.id < index - 2 || c.id > index + 4)
                            && (_Chunks[index].ticktime - c.ticktime).TotalSeconds > 4)
                        {
                            c.data = null;
                        }
                    }
                }
                return _Chunks[index];
            }
            return null;
        }


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => DecLength;

        public override long Position { get => DecPosition; set => DecPosition = value; }

        //mehhh this is inherent in the encryption and decryption
        public override void Flush()
        {
        }

        /// <summary>
        /// Read "length" bytes out of this stream, from the current position, into the buffer, starting at the given index.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int start, int length)
        {
            var _vp = DecPosition;
            if (DecPosition < 0 || DecPosition >= DecLength)
            {
                return 0;
            }
            else
            {
                //we could potentially get messed up super bad otherwise
                lock (this)
                {
                    long target = DecPosition + length;
                    if (target > DecLength) return 0;

                    //find the start of the virtual and encrypted ranges
                    long pos = start;
                    long read_remain = long.Min(length, DecLength - DecPosition);
                    int read = 0;

                    //read until done
                    while (read_remain > 0)
                    {
                        //calculate the index and get the chunk
                        int index = (int)(DecPosition / CHUNK_SIZE);
                        var e = GetChunk(DecPosition);
                        if (e == null)
                            return 0;
                        //calculate the offset of the block in the file
                        var block_offset = DecPosition - e.dec_offset;
                        var to_read = long.Min(e.dec_length - block_offset, read_remain);
                        if (e.data != null)
                            Array.Copy(e.data, block_offset, buffer, pos, to_read);

                        //increment the read pointers
                        DecPosition += to_read;
                        pos += to_read;
                        //consume the goal and count it
                        read += (int)to_read;
                        read_remain -= to_read;
                    }
                    return read;
                }
            }
        }

        /// <summary>
        /// Seeks the stream to the given position
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (this)
            {
                if (origin == SeekOrigin.Begin)
                {
                    DecPosition = offset;
                }
                else if (origin == SeekOrigin.Current)
                {
                    DecPosition += offset;
                }
                else if (origin == SeekOrigin.End)
                {
                    DecPosition = Length - offset;
                }
                return DecPosition;
            }

        }

        public override void SetLength(long value)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }
    }
}
