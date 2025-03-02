using System.Data;
using System.Data.SqlClient;
using System.Net.Sockets;
using Dapper;

namespace TetraClashServer
{
    public class Database
    {
        protected const string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClash;Trusted_Connection=True;";
        private IDbConnection DB;

        private Server Server;
        public Database(Server server)
        {
            DB = new SqlConnection(connectionString);
            const string checkExistsQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'Players'";

            const string createTableQuery = @"
            CREATE TABLE Players (
                Username NVARCHAR(50) NOT NULL PRIMARY KEY,
                Hash NVARCHAR(MAX) NOT NULL,
                Salt NVARCHAR(MAX) NOT NULL,
                Rating INT NOT NULL DEFAULT 1000
            );";


            int tableCount = DB.QuerySingleOrDefault<int>(checkExistsQuery);
            if (tableCount == 0)
            {
                DB.Execute(createTableQuery);
            }
            Server = server;
        }

        public async Task<string> CreateAccount(string message)
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

            try
            {
                var existingUser = await DB.QuerySingleOrDefaultAsync<string>(checkQuery, new { Username = username });
                if (existingUser != null)
                {
                    return "Player Exists";
                }

                int rowsAffected = await DB.ExecuteAsync(insertQuery, new { Username = username, Hash = hash, Salt = salt });
                Console.WriteLine($"{rowsAffected} row(s) inserted.");
                return $"Success:";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return $"Error: {e.Message}";
            }
        }

        public async Task<string> FetchSalt(string username)
        {
            try
            {
                string query = "SELECT Salt FROM Players WHERE Username = @Username";

                var salt = await DB.QuerySingleOrDefaultAsync<string>(query, new { Username = username });

                return salt ?? "Username";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public async Task<string> VerifyPlayer(TcpClient client, string message)
        {
            string[] args = message.Split(':');
            if (args.Length < 2)
            {
                return "Invalid message format.";
            }

            string username = args[0];
            string hashAttempt = args[1];

            try
            {
                string query = "SELECT Hash, Rating FROM Players WHERE Username = @Username";

                var result = await DB.QuerySingleOrDefaultAsync(query, new { Username = username });
                if (result != null)
                {
                    string hash = result.Hash;
                    int rating = result.Rating;
                    if (hashAttempt == hash)
                    {
                        Server.LoggedInPlayers.Add(client, username);
                        return $"Success:{rating}";
                    }
                    else
                    {
                        return "Password";
                    }
                }
                return "Database Error";
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return e.Message;
            }
        }
        public static async Task<int> CalculateEloChange(string winnerName, string loserName)
        {
            // Query to fetch a single rating value.
            string query = "SELECT Rating FROM Players WHERE Username = @Username";

            // Fetch the winner's rating.
            int winnerRating = await DB.QuerySingleOrDefaultAsync<int>(query, new { Username = winnerName });

            // Fetch the loser's rating.
            int loserRating = await DB.QuerySingleOrDefaultAsync<int>(query, new { Username = loserName });

            // Optionally, check if either rating was not found.
            if (winnerRating == default(int) || loserRating == default(int))
            {
                throw new Exception("Could not find rating for one or both users.");
            }

            // Compute the rating difference.
            double ratingDifference = winnerRating - loserRating;

            // Cap the difference to ±1000 rating points.
            ratingDifference = Math.Max(-1000, Math.Min(1000, ratingDifference));

            // Calculate the adjustment:
            // Base 30 points, subtract/add 10 points per 1000 points difference.
            double adjustment = 30.0 - ((ratingDifference / 1000.0) * 10.0);

            // Clamp the adjustment between 20 (minimum win) and 40 (big upset bonus).
            adjustment = Math.Max(20, Math.Min(adjustment, 40));

            return (int)Math.Round(adjustment);
        }
    }
}
