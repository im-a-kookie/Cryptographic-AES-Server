using System.Diagnostics;
using System.Security.Cryptography;


namespace VideoPlayer
{

    public class AesCtrStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly Aes _aes;

        private readonly ICryptoTransform _encryptor;

        private readonly byte[] _counter;
        private readonly byte[] _counterBuffer;
        private readonly byte[] _keyStream;

        private long _position;
        private int _keyStreamPos;

        


        public AesCtrStream(Stream baseStream, byte[] key, byte[] iv)
        {
            _baseStream = baseStream;
            _aes = Aes.Create();
            _aes.Key = key;
            _aes.IV = iv;
            //ECB is kinda cool, but not the safest if we aren't careful
            //However we can use an external Counter
            //Which will prevent this from becoming a problem
            _aes.Mode = CipherMode.ECB;
            _aes.Padding = PaddingMode.None;

            //Create the encryptor and decryptor
            _encryptor = _aes.CreateEncryptor();
            //_decryptor = _aes.CreateDecryptor();

            //So here we make a counter from the IV array
            _counter = (byte[])iv.Clone();

            //remember the block size is in bits not bytes, so we need to pack it. Oops.
            _counterBuffer = new byte[_aes.BlockSize / 8];
            _keyStream = new byte[_aes.BlockSize / 8];
            _position = 0;
            _keyStreamPos = _keyStream.Length; // Force initial key stream generation
        }

        /// <summary>
        /// Increments the counter array
        /// </summary>
        private void IncrementCounter()
        {
            //increment from least significant byte
            for (int i = _counter.Length - 1; i >= 0; --i)
            {
                if (++_counter[i] != 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Generates the next decrypted block
        /// </summary>
        private void GenerateKeyStream()
        {
            //transform the 
            _encryptor.TransformBlock(_counter, 0, _counter.Length, _keyStream, 0);
            IncrementCounter();
            _keyStreamPos = 0;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                //make sure the buffers are ready
                if (_keyStreamPos == _keyStream.Length) GenerateKeyStream();
                int blockSize = Math.Min(_keyStream.Length - _keyStreamPos, count);

                //AES essentially creates a really messy spammy XOR which scrambles up the block beyond all recognition
                for (int i = 0; i < blockSize; i++)
                    _counterBuffer[i] = (byte)(buffer[offset + i] ^ _keyStream[_keyStreamPos + i]);
                

                //now write the buffer to the base stream
                _baseStream.Write(_counterBuffer, 0, blockSize);

                _keyStreamPos += blockSize;
                offset += blockSize;
                count -= blockSize;
                _position += blockSize;
            }

        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (count > 0)
            {
                if (_keyStreamPos == _keyStream.Length) GenerateKeyStream();
                
                //now we reverse the above,
                //get the block size, or the amount of stuff left to read from the block
                int blockSize = Math.Min(_keyStream.Length - _keyStreamPos, count);
                int baseStreamRead = _baseStream.Read(_counterBuffer, 0, blockSize);
                if (baseStreamRead == 0) break;

                //Now Xor it the other way to get the original data
                for (int i = 0; i < baseStreamRead; i++)
                    buffer[offset + i] = (byte)(_counterBuffer[i] ^ _keyStream[_keyStreamPos + i]);



                //now we just update the position and we're good
                _keyStreamPos += baseStreamRead;
                offset += baseStreamRead;
                count -= baseStreamRead; //counting down
                bytesRead += baseStreamRead;
                _position += baseStreamRead;
            }

            return bytesRead;
        }


        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = _baseStream.Seek(offset, origin);
            if (newPos < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            
            _position = newPos;
            long blockNumber = _position / (_aes.BlockSize / 8);
            //reset the counter from the IV and then churn through and put it to how it should be
            Array.Copy(_aes.IV, _counter, _aes.BlockSize / 8);
            for (int i = 0; i < blockNumber; i++) IncrementCounter();
            //regenerate the current key stream
            GenerateKeyStream();
            //now put us at the right place in the block
            _keyStreamPos = (int)(_position - (blockNumber * (_aes.BlockSize / 8)));
            return newPos;
        }

        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _encryptor.Dispose();
                _aes.Dispose();
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
