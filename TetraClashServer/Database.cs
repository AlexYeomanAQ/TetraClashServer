using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Security.Cryptography;

namespace TetraClashServer
{
    class Database
    {
        public static void CreateAccount (TcpClient client, string message)
        {
            string username = message.Substring (0, message.IndexOf(':'));
            string password = message.Substring(message.IndexOf(":"));

            string salt = GenerateSalt();

            string hashedPassword = HashPassword(password, salt);

            string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

            string insertQuery = $"INSERT INTO Player (Name, Salt, Password) VALUES ({username}, {salt}, {password})";

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                int rowsAffected = db.Execute(insertQuery);
                Console.WriteLine($"{rowsAffected} row(s) inserted.");
            }
        }
        public class PlayerCredentials
        {
            public string Salt { get; set; }
            public string Password { get; set; }
        }
        public static bool VerifyPlayer(TcpClient client, string username, string password)
        {
            string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

            try
            {
                using (IDbConnection db = new SqlConnection(connectionString))
                {
                    db.Open();

                    string query = $"SELECT Salt, Password FROM Players WHERE Username = {username}";

                    PlayerCredentials credentials = db.QuerySingleOrDefault<PlayerCredentials>(query);

                    if (credentials != null)
                    {
                        string checkPassword = HashPassword(password, credentials.Salt);
                        if (checkPassword == credentials.Password)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            RandomNumberGenerator.Fill(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        static string HashPassword(string password, string salt)
        {
            string saltedPassword = password + salt;
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashBytes);
        }

    }
}
