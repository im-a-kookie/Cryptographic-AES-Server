using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;


namespace VideoPlayer
{

    public class AesHelper
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


        public static void EncryptFile(string input, string output, string password)
        {
            using (FileStream ins = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (File.Exists(output)) File.Delete(output);
                using (FileStream outs = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var kv = DeriveKeyFromPassword(password);
                    using (AesCtrStream cs = new AesCtrStream(outs, kv.Key, kv.IV))
                    {
                        ins.CopyTo(cs);
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
        /// <param name="text"></param>
        /// <returns></returns>
        public static string SHA256Password(string text)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns the SHA256 hash of the given file, by path
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string SHA256File(string filePath)
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



    }
}
