using System.Security.Cryptography;
using System.Text;

namespace VirtualPaper.Common.Utils.Security {
    public static class AESHelper {
        /// <summary>
        /// AES 加密
        /// </summary>
        /// <param name="plainText">待加密字符串</param>
        /// <returns></returns>
        /// <summary>
        public static string EncryptStringToBytes_Aes(string plainText) {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));

            string encrypted = null;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create()) {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream()) {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write)) {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt)) {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        /// <summary>
        /// AES 解密
        /// </summary>
        /// <param name="cipherText">待解密字符串</param>
        /// <returns></returns>
        public static string DecryptStringFromBytes_Aes(string cipherText) {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException(nameof(cipherText));

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create()) {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText))) {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read)) {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt)) {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        private static readonly string key = "16504vpiarpteurahlapmampeerr1650";
        /// <summary>
        /// 对称算法的密钥
        /// </summary>
        private static readonly byte[] Key = Encoding.UTF8.GetBytes(key);

        private static readonly string iv = "16504HAMMER16504";
        /// <summary>
        /// 对称算法的初始化向量
        /// </summary>
        private static readonly byte[] IV = Encoding.UTF8.GetBytes(iv);
    }
}
