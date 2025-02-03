using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace TetraClashServer
{
    class Database
    {
        protected const string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClashTest;Trusted_Connection=True;";

        public static async Task<bool> Initialize()
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
                    int tableCount = await db.QuerySingleOrDefaultAsync<int>(checkExistsQuery);
                    if (tableCount == 0)
                    {
                        await db.ExecuteAsync(createTableQuery);
                    }
                    Console.WriteLine("Database initialized");
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Database failed to initialize: " + e);
                    return false;
                }
            }
        }

        public static async Task<string> CreateAccount(string message)
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
                    string existingUser = await db.QuerySingleOrDefaultAsync<string>(checkQuery, new { Username = username });
                    if (existingUser != null)
                    {
                        return "Player Exists";
                    }

                    int rowsAffected = await db.ExecuteAsync(insertQuery, new { Username = username, Hash = hash, Salt = salt });
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

        public static async Task<string> FetchSalt(string username)
        {
            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    string query = "SELECT Salt FROM Players WHERE Username = @Username";

                    var salt = await db.QuerySingleOrDefaultAsync<string>(query, new { Username = username });

                    return salt ?? "Username";
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }
        }

        public static async Task<string> VerifyPlayer(string message)
        {
            string[] args = message.Split(':');
            if (args.Length < 2)
            {
                return "Invalid message format.";
            }

            string username = args[0];
            string hashAttempt = args[1];

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                try
                {
                    string query = "SELECT Hash FROM Players WHERE Username = @Username";

                    string hash = await db.QuerySingleOrDefaultAsync<string>(query, new { Username = username });

                    return hashAttempt == hash ? "Success" : "Password";
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
