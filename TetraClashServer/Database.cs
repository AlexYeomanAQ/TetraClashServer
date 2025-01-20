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
        protected const string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

        public static bool Initialize()
        {
            const string checkExistsQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'Players'";

            const string createTableQuery = @"
            CREATE TABLE Players (
                Username NVARCHAR(50) NOT NULL PRIMARY KEY,
                Hash NVARCHAR(MAX) NOT NULL,
                Salt NVARCHAR(MAX) NOT NULL
            );";

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    int tableCount = db.QuerySingleOrDefault<int>(checkExistsQuery);
                    if (tableCount == 0)
                    {
                        db.Execute(createTableQuery);
                    }
                    Console.WriteLine("Database initialized");
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Database fail to initialise: " + e);
                    return false;
                }
            }
        }

        public static string CreateAccount(string message)
        {
            string[] args = message.Split(":");
            if (args.Length < 3)
            {
                return "Invalid message format.";
            }

            string username = args[0];
            string hash = args[1];
            string salt = args[2];

            string insertQuery = "INSERT INTO Players (Username, Hash, Salt) VALUES (@Username, @Hash, @Salt)";
            string checkQuery = "SELECT Username FROM Players WHERE Username = @Username";

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    string existingUser = db.QuerySingleOrDefault<string>(checkQuery, new { Username = username });
                    if (existingUser != null)
                    {
                        return "Player Exists";
                    }

                    int rowsAffected = db.Execute(insertQuery, new { Username = username, Hash = hash, Salt = salt });
                    Console.WriteLine($"{rowsAffected} row(s) inserted.");
                    return "Success";
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                    return $"Error: {e.Message}";
                }
            }
        }

        public static string FetchSalt(string username)
        {
            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    db.Open();
                    string query = $"SELECT Salt FROM Players WHERE Username = @Username";

                    var salt = db.QuerySingleOrDefault<string>(query, new { Username = username });

                    if (salt == null)
                    {
                        return "Username";
                    }

                    return salt;
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
        }
        public static string VerifyPlayer(string message)
        {
            string[] args = message.Split(':');
            string username = args[0];
            string hashAttempt = args[1];

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    db.Open();

                    string query = $"SELECT Hash FROM Players WHERE Username = @Username";

                    string hash = db.QuerySingle<string>(query, new {Username = username});

                    if (hashAttempt == hash)
                    {
                        return "Success";
                    }
                    else
                    {
                        return "Password";
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return e.Message;
                }
            }
        }
    }
}
