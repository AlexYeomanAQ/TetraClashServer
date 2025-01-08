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
            string[] args = message.Split(":");
            string username = args[0];
            string hash = args[1];
            string salt = args[2];

            string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

            string insertQuery = $"INSERT INTO Player (Username, Hash, Salt) VALUES ({username}, {hash}, {salt})";

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                string checkQuery = $"SELECT Username FROM Players WHERE Username = {username}";

                string credentials = db.QuerySingleOrDefault<string>(checkQuery);

                if (credentials != null)
                {
                    int rowsAffected = db.Execute(insertQuery);
                    Console.WriteLine($"{rowsAffected} row(s) inserted.");
                    
                }
                else return "Player Exists";
            }
        }
        public class PlayerCredentials
        {
            public string Hash { get; set; }
            public string Salt { get; set; }
        }
        public static string VerifyPlayer(TcpClient client, string message)
        {
            string[] args = message.Split(":");
            string username = args[1];
            string password = args[2];

            string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

            try
            {
                using (IDbConnection db = new SqlConnection(connectionString))
                {
                    db.Open();

                    string query = $"SELECT Hash, Salt FROM Players WHERE Username = {username}";

                    var credentials = db.QuerySingleOrDefault<PlayerCredentials>(query);

                    if (credentials != null)
                    {
                        string hashAttempt = HashPassword(password, credentials.Salt);
                        if (hashAttempt == credentials.Hash)
                        {
                            return "Success";
                        }
                        else
                        {
                            return "Password";
                        }
                    }
                    else
                    {
                        return "Username";
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
