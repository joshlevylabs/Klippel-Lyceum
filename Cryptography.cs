using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using static LAPxv8.FormAudioPrecision8;

namespace LAPxv8
{
    public static class Cryptography
    {
        public static string GetOrCreateEncryptionKey()
        {
            string predefinedKey = "Lyceum2024";
            string environmentKey = Environment.GetEnvironmentVariable("LYCEUM_APP_KEY", EnvironmentVariableTarget.Machine);

            if (string.IsNullOrEmpty(environmentKey))
            {
                LogManager.AppendLog($"❌ ERROR: Environment variable 'LYCEUM_APP_KEY' is not set.");
                return null;
            }

            string encryptedPredefinedKey = ConvertToBase64(predefinedKey);
            string encryptedEnvironmentKey = ConvertToBase64(environmentKey);

            if (encryptedPredefinedKey != encryptedEnvironmentKey)
            {
                LogManager.AppendLog($"❌ ERROR: Encryption key validation failed.");
                LogManager.AppendLog($"🔑 Predefined Key: {predefinedKey}, Length: {predefinedKey.Length}");
                LogManager.AppendLog($"🔑 Environment Key Length: {environmentKey.Length}");
                return null;
            }

            LogManager.AppendLog($"✅ Encryption key retrieved and validated successfully.");
            return encryptedEnvironmentKey;
        }


        public static string EncryptString(string key, string plainText)
        {
            try
            {
                LogManager.AppendLog($"[EncryptString] 🔒 Starting encryption process.");

                byte[] keyBytes = GetAESKey(key);
                byte[] iv = GenerateIV();

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(iv, 0, iv.Length); // Store IV in output
                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        using (StreamWriter writer = new StreamWriter(cs))
                        {
                            writer.Write(plainText);
                        }

                        byte[] encryptedBytes = ms.ToArray();
                        LogManager.AppendLog($"✅ Encryption successful. Encrypted length: {encryptedBytes.Length} bytes");
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR: Encryption failed - {ex.Message}");
                return null;
            }
        }

        public static string DecryptString(string key, string cipherText)
        {
            try
            {
                LogManager.AppendLog($"[DecryptString] 🔓 Starting decryption process.");

                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                LogManager.AppendLog($"🔍 Encrypted Data Length: {cipherBytes.Length} bytes");

                if (cipherBytes.Length < 16)
                {
                    LogManager.AppendLog($"❌ ERROR: Ciphertext is too short for AES decryption.");
                    return null;
                }

                byte[] iv = new byte[16];
                Array.Copy(cipherBytes, 0, iv, 0, 16);
                byte[] encryptedBytes = new byte[cipherBytes.Length - 16];
                Array.Copy(cipherBytes, 16, encryptedBytes, 0, encryptedBytes.Length);

                byte[] keyBytes = GetAESKey(key);
                LogManager.AppendLog($"🔑 AES Key Length: {keyBytes.Length} bytes, IV Length: {iv.Length} bytes");

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (MemoryStream ms = new MemoryStream(encryptedBytes))
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    using (StreamReader reader = new StreamReader(cs))
                    {
                        string decryptedText = reader.ReadToEnd();

                        LogManager.AppendLog($"🔓 Decryption Output:\n{decryptedText}");

                        if (string.IsNullOrWhiteSpace(decryptedText) || decryptedText.Length < 20)
                        {
                            LogManager.AppendLog("❌ ERROR: Decryption returned empty or invalid data.");
                            return null;
                        }

                        try
                        {
                            JsonConvert.DeserializeObject(decryptedText);
                        }
                        catch (JsonException jsonEx)
                        {
                            LogManager.AppendLog($"❌ ERROR: Decryption output is not valid JSON. {jsonEx.Message}");
                            return null;
                        }

                        LogManager.AppendLog($"✅ Decryption successful. Decrypted text length: {decryptedText.Length} bytes");
                        return decryptedText;
                    }
                }
            }
            catch (CryptographicException ex)
            {
                LogManager.AppendLog($"❌ ERROR: CryptographicException - {ex.Message}");
                MessageBox.Show("Decryption failed due to an invalid key or corrupted data.", "Decryption Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            catch (Exception ex)
            {
                LogManager.AppendLog($"❌ ERROR: General Exception during decryption - {ex.Message}");
                return null;
            }
        }

        private static byte[] GetAESKey(string key)
        {
            byte[] keyBytes = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(key)); // AES-128 requires a 16-byte key
            LogManager.AppendLog($"🔑 Generated AES Key Length: {keyBytes.Length} bytes");
            return keyBytes;
        }

        public static bool IsValidBase64String(string base64)
        {
            // Log the key for debugging purposes. Remove this in production.
            //AppendLog($"Key being validated: {base64}\n");

            if (string.IsNullOrEmpty(base64) || base64.Length % 4 != 0
               || base64.Contains(" ") || base64.Contains("\t") || base64.Contains("\r") || base64.Contains("\n"))
                return false;

            try
            {
                Convert.FromBase64String(base64);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static byte[] GenerateIV()
        {
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(iv);
            }
            return iv;
        }

        private static string ConvertToBase64(string input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }
    }
}
