using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace WpfApp1
{
    public class DatabaseConfig
    {
        public string Server { get; set; } = "localhost";
        public string User { get; set; } = "root";

        private string? _encryptedPassword;

        [JsonIgnore]
        public string Password { get; set; } = "";

        public string? EncryptedPassword
        {
            get => _encryptedPassword;
            set => _encryptedPassword = value;
        }

        public string Database { get; set; } = "pokemon_2025";
        public int Port { get; set; } = 3306;
        
        // Track last price update date
        public DateTime? LastPriceUpdate { get; set; }

        private static string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PokemonCardCollection",
            "dbconfig.json"
        );

        private static byte[] GetEncryptionKey ()
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName));
        }

        public string GetConnectionString ()
        {
            return $"server={Server};user={User};password={Password};database={Database};port={Port};";
        }

        public string GetConnectionStringWithoutDb ()
        {
            return $"server={Server};user={User};password={Password};port={Port};";
        }

        public static DatabaseConfig Load ()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<DatabaseConfig>(json) ?? new DatabaseConfig();

                    if (!string.IsNullOrEmpty(config.EncryptedPassword))
                    {
                        config.Password = DecryptPassword(config.EncryptedPassword);
                    }

                    return config;
                }
            }
            catch
            {
                // Failed to load config, return default
            }

            return new DatabaseConfig();
        }

        public void Save ()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigFilePath)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!string.IsNullOrEmpty(Password))
                {
                    _encryptedPassword = EncryptPassword(Password);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch
            {
                // Failed to save config
            }
        }

        private static string EncryptPassword (string password)
        {
            byte[] key = GetEncryptionKey();
            byte[] iv = new byte[16];
            RandomNumberGenerator.Fill(iv);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] encrypted = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

            byte[] result = new byte[iv.Length + encrypted.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);

            return Convert.ToBase64String(result);
        }

        private static string DecryptPassword (string encryptedPassword)
        {
            try
            {
                byte[] key = GetEncryptionKey();
                byte[] fullData = Convert.FromBase64String(encryptedPassword);

                byte[] iv = new byte[16];
                byte[] encrypted = new byte[fullData.Length - 16];
                Buffer.BlockCopy(fullData, 0, iv, 0, 16);
                Buffer.BlockCopy(fullData, 16, encrypted, 0, encrypted.Length);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool TestConnection (out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                using (var conn = new MySql.Data.MySqlClient.MySqlConnection(GetConnectionStringWithoutDb()))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}