using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoPlayer
{
    internal class EStream : Stream
    {

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

        private Stream IOStream;
        private (byte[] Key, byte[] Iv) cached_key;
        private EChunk[]? _Chunks;

        /// <summary>
        /// The imaginary position in the decrypted file
        /// </summary>
        private long DecPosition;

        private long EncryptedPosition => IOStream.Position;


        /// <summary>
        /// The imaginary length of the decrypted file
        /// </summary>
        public long DecLength;


        public class ProgressObject
        {
            public bool completed = false;
            public double progress = 0d;
            public Action? Callback;
            public void AwaitCompletion()
            {
                while (!completed)
                {
                    Thread.Sleep(5);
                }
            }
            public void notify()
            {
                completed = true;
                Callback?.Invoke();
            }
        }

        public static ProgressObject EncryptFile(string input, string output, string key)
        {
            ProgressObject po = new ProgressObject();
            Task.Run(() =>
            {
                using (FileStream fi = File.OpenRead(input))
                {
                    using (FileStream fo = File.OpenWrite(output))
                    {
                        var k = AesEncryption.DeriveKeyFromPassword(key);

                        //now we need to chunk the file
                        int chunks = (int)double.Ceiling(fi.Length / (double)CHUNK_SIZE);

                        //now we know there are gonna be 4 + (chunks * 8) writes
                        int offset = sizeof(int) + chunks * 2 * sizeof(long);
                        byte[] header = new byte[offset];
                        fo.Write(header, 0, offset);

                        //now let's start
                        int pos = 0;
                        byte[] block = new byte[CHUNK_SIZE];
                        double processed = 0;

                        List<(long elength, long dlength)> stuff = new();
                        for (int i = 0; i < chunks; i++)
                        {

                            bool done = false;
                            int read = (int)long.Min(fi.Length - pos, block.LongLength);
                            if (read != block.Length)
                            {
                                block = new byte[read];
                            }

                            fi.Read(block, 0, read);

                            var ec = AesEncryption.Encrypt(block, k);
                            pos += read;
                            //Debug.WriteLine("Encrypted " + stuff.Count + ": Vp=" + pos + ", ep=" + fo.Position + ", h=" + AesEncryption.ComputeSHA1Hash(block));

                            fo.Write(ec, 0, ec.Length);

                            stuff.Add((ec.Length, block.Length));
                            if (po != null) po.progress = (double)++processed / chunks;

                            if (done) break;
                        }

                        fo.Seek(0, SeekOrigin.Begin);
                        using (BinaryWriter bw = new BinaryWriter(fo))
                        {
                            bw.Write(chunks);
                            foreach (var s in stuff)
                            {
                                bw.Write(s.elength);
                                bw.Write(s.dlength);
                            }
                            //Debug.WriteLine("Wrote " + fo.Position + " bytes header (expected: " + offset + ")");
                        }



                    }
                }
                po?.notify();
            });

            return po;

        }




        public EStream(Stream inputStream, string key)
        {
            cached_key = AesEncryption.DeriveKeyFromPassword(key);
            IOStream = inputStream;
            ReadHeader();
        }


        public static EStream Read(Stream inputStream, string key)
        {
            EStream es = new EStream(inputStream, key);
            //read the header
            return es;
        }

        public void ReadHeader()
        {
            try
            {
                IOStream.Seek(0, SeekOrigin.Begin);
                //read the number of chunks
                BinaryReader br = new BinaryReader(IOStream);
                _Chunks = new EChunk[br.ReadInt32()];
                DecLength = 0;
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
                Position = 0;
            }
            catch
            {
                //...
            }

        }


        private EChunk? GetChunk(long pos)
        {
            if (_Chunks == null) return null;
            int index = (int)(pos / CHUNK_SIZE);
            if (index == _Chunks.Length) index -= 1;
            if (index < 0 || index >= _Chunks.Length) return null;

            if (_Chunks != null)

            {
                if (_Chunks[index].data == null)
                {
                    //grab the encrypted block
                    byte[] data = new byte[_Chunks[index].elength];
                    IOStream.Position = _Chunks[index].epos;
                    IOStream.Read(data, 0, data.Length);

                    _Chunks[index].data = AesEncryption.Decrypt(data, cached_key);

                    //Debug.WriteLine("Reading " + index + ": vp=" + _Chunks[index].dpos + ", ep=" + _Chunks[index].epos + ", h=" + AesEncryption.ComputeSHA1Hash(_Chunks[index].data));

                    _Chunks[index].ticktime = DateTime.UtcNow;
                    //clean old chunks
                    foreach (var c in _Chunks)
                    {
                        if (c.data != null
                            && c != _Chunks[index]
                            //keep the old 1-2 chunks and the next few chunks here ready
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

        public override void Flush()
        {
        }

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
                        int index = (int)(DecPosition / CHUNK_SIZE);
                        var e = GetChunk(DecPosition);
                        if (e == null)
                            return 0;

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
