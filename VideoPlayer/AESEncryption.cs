using System.Security.Cryptography;
using System.Text;


namespace VideoPlayer
{

    public class AesEncryption
    {
        /// <summary>
        /// Keysize in bits (e.g 256 bit)
        /// </summary>
        private static readonly int KeySize = 256;

        /// <summary>
        /// Block size for encryption
        /// </summary>
        private static readonly int BlockSize = 128;

        /// <summary>
        /// Salt length in bytes
        /// </summary>
        private static readonly int SaltLength = 16;

        /// <summary>
        /// The number of iterations for the password generator
        /// </summary>
        private static readonly int Iterations = 12415;

        /// <summary>
        ///  Here's a random Salt. We could do more to protect this in production, but it's NBD
        /// </summary>
        public static byte[] Salt = [183, 42, 199, 97, 158, 23, 4, 210, 126, 89, 172, 54, 101, 207, 68, 194];


        /// <summary>
        /// Creates an AES stream wrapping the given stream.
        /// </summary>
        /// <param name="outputStream">The stream to write encrypted data to</param>
        /// <param name="password">plaintext password</param>
        /// <returns></returns>
        public static CryptoStream CreateEncryptionStream(Stream outputStream, string password)
        {
            var key = DeriveKeyFromPassword(password);
            return CreateEncryptionStream(outputStream, key);
        }

        /// <summary>
        /// Creates an AES stream wrapping the given stream
        /// </summary>
        /// <param name="outputStream">The stream to write encrypted data to</param>
        /// <param name="key">Key/IV in 16 byte arrays</param>
        /// <returns></returns>
        public static CryptoStream CreateEncryptionStream(Stream outputStream, (byte[] Key, byte[] IV) key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Key = key.Key;
                aes.IV = key.IV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                return new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
            }
        }

        /// <summary>
        /// Creates an AES stream wrapping a given input stream
        /// </summary>
        /// <param name="inputStream">The stream to read encrypted data from</param>
        /// <param name="password">plaintext password</param>
        /// <returns></returns>
        public static CryptoStream CreateDecryptionStream(Stream inputStream, string password)
        {
            var key = DeriveKeyFromPassword(password);
            return CreateDecryptionStream(inputStream, key);
        }
        public static CryptoStream CreateDecryptionStream(Stream inputStream, (byte[] Key, byte[] IV) key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.BlockSize = BlockSize;
                aes.Key = key.Key;
                aes.IV = key.IV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                return new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            }
        }

        /// <summary>
        /// Encrypts the given byte array and returns the result on the fly
        /// <para>Symmetrical with <see cref="Decrypt(byte[], string)"/></para>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] data, string key)
        {
            return Encrypt(data, DeriveKeyFromPassword(key));
        }

        /// <summary>
        /// Encrypts the given byte array and returns the result on the fly
        ///<para>Symmetrical with <see cref="Decrypt(byte[], ValueTuple{byte[], byte[]})"/></para>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] data, (byte[] key, byte[] iv) key)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key.key;
                aesAlg.IV = key.iv;

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, aesAlg.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(data, 0, data.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        /// <summary>
        /// Decrypts the given byte array and returns the result on the fly.
        /// <para>Symmetrical with <see cref="Encrypt(byte[], string)"/></para>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] data, string key)
        {
            return Decrypt(data, DeriveKeyFromPassword(key));
        }

        /// <summary>
        /// Decrypts the given byte array and returns the result on the fly.
        /// <para>Symmetrical with <see cref="Encrypt(byte[], ValueTuple{byte[], byte[]})"/></para>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] encryptedData, (byte[] key, byte[] iv) key)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key.key;
                aesAlg.IV = key.iv;

                using (MemoryStream msDecrypt = new MemoryStream(encryptedData))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, aesAlg.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            csDecrypt.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Derives a byte/IV key pair from the input password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static (byte[] Key, byte[] IV) DeriveKeyFromPassword(string password, int iterations = -1)
        {
            using (var keyDerivationFunction = new Rfc2898DeriveBytes(
                password,
                Salt, 
                iterations < 0 ? Iterations : iterations, 
                HashAlgorithmName.SHA512))
            {
                byte[] key = keyDerivationFunction.GetBytes(KeySize / 8);
                byte[] iv = keyDerivationFunction.GetBytes(BlockSize / 8);
                return (key, iv);
            }
        }

        /// <summary>
        /// Returns the SHA256 hash for a given input string
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string SHA256Password(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns the SHA256 hash of the given file, by path
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string ComputeSHA256(string filePath)
        {
            using (FileStream fileStream = File.OpenRead(filePath))
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(fileStream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

    }
}
