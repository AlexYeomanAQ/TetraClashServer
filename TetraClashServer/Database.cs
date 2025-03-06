using System.Data;
using System.Data.SqlClient;
using System.Net.Sockets;
using Dapper;

namespace TetraClashServer
{
    public class Database
    {
        protected const string connectionString = $"Server=localhost\\MSSQLSERVER01;Database=TetraClash;Trusted_Connection=True;MultipleActiveResultSets=True";
        private IDbConnection DB;

        private Server Server;
        public Database(Server server)
        {
            DB = new SqlConnection(connectionString);
            const string checkExistsQuery = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_NAME = 'Players'";

            const string createPlayerTableQuery = @"
            CREATE TABLE Players (
                Username NVARCHAR(50) NOT NULL PRIMARY KEY,
                Hash NVARCHAR(MAX) NOT NULL,
                Salt NVARCHAR(MAX) NOT NULL,
                Rating INT NOT NULL DEFAULT 1000
            );";

            const string createHighscoreTableQuery = @"
            CREATE TABLE Highscores (
                Username NVARCHAR(50) NOT NULL,
                Date DATETIME NOT NULL DEFAULT GETDATE(),
                Score INT NOT NULL,
                PRIMARY KEY (Username, Date),
                FOREIGN KEY (Username) REFERENCES Players(Username)
            );";


            int tableCount = DB.QuerySingleOrDefault<int>(checkExistsQuery);
            if (tableCount == 0)
            {
                DB.Execute(createPlayerTableQuery);
            }
            Server = server;
        }

        public async Task<string> CreateAccount(TcpClient client, string message)
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
                Server.LoggedInPlayers.Add(client, username);
                return $"Success:1000";
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
                        if (Server.LoggedInPlayers.Values.ToArray().Contains(username))
                        {
                            return $"Logged In";
                        }
                        Server.LoggedInPlayers.Add(client, username);
                        return $"Success{rating}";
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
        public async Task<int> CalculateEloChange(string winnerName, string loserName, bool tied = false)
        {
            string query = "SELECT Rating FROM Players WHERE Username = @Username"; // Query to fetch a single rating value.

            int winnerRating = await DB.QuerySingleOrDefaultAsync<int>(query, new { Username = winnerName }); // Fetch the winner's rating.
            int loserRating = await DB.QuerySingleOrDefaultAsync<int>(query, new { Username = loserName }); // Fetch the loser's rating.

            double ratingDifference = winnerRating - loserRating; // Compute the rating difference.

            ratingDifference = Math.Max(-1000, Math.Min(1000, ratingDifference)); // Cap the difference to ±1000 rating points.

            int baseVal = tied ? 0 : 30; //If tied, base is 0, else is 30
            double adjustment = baseVal - (ratingDifference / 100); // Base points, subtract/add 10 points per 1000 points difference.

            // Clamp the adjustment 10 above or below baseval, to prevent large swings for big rating differtences.

            adjustment = Math.Max(baseVal - 10, Math.Min(adjustment, baseVal + 10));

            await UpdateRatings(winnerName, loserName, (int)Math.Round(adjustment));

            return (int)Math.Round(adjustment);
        }

        public async Task UpdateRatings(string winnerName, string loserName, int adjustment)
        {
            string query = @"
            UPDATE Players
            SET rating = 
                CASE 
                    WHEN Username = @winnerName THEN rating + @adjustment
                    WHEN Username = @loserName THEN rating - @adjustment
                END
            WHERE Username IN (@winnerName, @loserName);";

            try
            {
                await DB.ExecuteAsync(query, new { winnerName, loserName, adjustment });
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync("Unknown Error: " +ex );
            }
        }

        public async Task UpdateHighscore(string username, int score)
        {
            List<(int Score, DateTime Date)> currentHighScores = FetchHighscores(username);
            if (currentHighScores.Count() == 10)
            {
                var lowestScore = currentHighScores[9];
                if (lowestScore.Score < score)
                {
                    string removeQuery = @"
                        DELETE FROM Highscores 
                        WHERE Username = @Username AND Score = @LowestScore AND Date = @LowestScoreDate";

                    DB.Execute(removeQuery, new { Username = username, LowestScore = lowestScore.Score, LowestScoreDate = lowestScore.Date });
                }
            }
            const string insertHighscoreQuery = @"
            INSERT INTO Highscores (Username, Score) 
            VALUES (@Username, @Score)";

            DB.Execute(insertHighscoreQuery, new { Username = username, Score = score });
        }

        public List<(int Score, DateTime Date)> FetchHighscores(string username)
        {
            try
            {
                const string fetchHighscoresQuery = @"
            SELECT Score, Date 
            FROM Highscores 
            WHERE Username = @Username";

                var highscores = DB.Query<(int Score, DateTime Date)>(fetchHighscoresQuery, new { Username = username }).ToList();
                return MergeSortHighscores(highscores);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<(int Score, DateTime Date)>();
            }
        }

        private List<(int Score, DateTime Date)> MergeSortHighscores(List<(int Score, DateTime Date)> highscores)
        {
            if (highscores.Count <= 1) return highscores;

            int mid = highscores.Count / 2;
            var left = highscores.GetRange(0, mid);
            var right = highscores.GetRange(mid, highscores.Count - mid);

            left = MergeSortHighscores(left);
            right = MergeSortHighscores(right);

            return Merge(left, right);
        }

        private List<(int Score, DateTime Date)> Merge(List<(int Score, DateTime Date)> left, List<(int Score, DateTime Date)> right)
        {
            var result = new List<(int Score, DateTime Date)>();
            int leftIndex = 0, rightIndex = 0;

            while (leftIndex < left.Count && rightIndex < right.Count)
            {
                if (left[leftIndex].Score > right[rightIndex].Score)
                {
                    result.Add(left[leftIndex]);
                    leftIndex++;
                }
                else
                {
                    result.Add(right[rightIndex]);
                    rightIndex++;
                }
            }

            result.AddRange(left.GetRange(leftIndex, left.Count - leftIndex));
            result.AddRange(right.GetRange(rightIndex, right.Count - rightIndex));

            return result;
        }
    }
}
