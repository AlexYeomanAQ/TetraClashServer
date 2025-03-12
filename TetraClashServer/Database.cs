using System.Data; // Import basic data access functionality
using System.Data.SqlClient; // Import SQL Server client functionality
using System.Net.Sockets; // Import networking sockets support
using Dapper; // Import Dapper micro-ORM for database operations

namespace TetraClashServer // Define the TetraClashServer namespace
{
    public class Match // Represents a match between two players
    {
        public string MatchID { get; set; } // Unique identifier for the match
        public TcpClient Player1 { get; set; } // TcpClient connection for Player1
        public TcpClient Player2 { get; set; } // TcpClient connection for Player2
        public int Player1Score { get; set; } // Score for Player1
        public int Player2Score { get; set; } // Score for Player2
    }

    // Custom DTO to represent a highscore.
    public class Highscore // Represents an individual highscore entry
    {
        public int Score { get; set; } // The achieved score
        public string Date { get; set; } // The date when the score was achieved (as a string)
    }

    public class Database // Provides database operations for the server
    {
        protected const string connectionString = "Server=localhost\\MSSQLSERVER01;Database=TetraClash;Trusted_Connection=True;MultipleActiveResultSets=True"; // Connection string for SQL Server
        private Server Server; // Reference to the server instance

        public Database(Server server) // Constructor that accepts a Server instance
        {
            Server = server; // Assign the server instance
            InitializeDatabase().Wait(); // Initialize the database synchronously
        }

        private async Task InitializeDatabase() // Initializes the database schema if not already present
        {
            const string checkExistsQuery = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = 'Players'"; // Query to check if the 'Players' table exists

            const string createPlayerTableQuery = @"
                CREATE TABLE Players (
                    Username NVARCHAR(50) NOT NULL PRIMARY KEY,
                    Hash NVARCHAR(MAX) NOT NULL,
                    Salt NVARCHAR(MAX) NOT NULL,
                    Rating INT NOT NULL DEFAULT 1000
                );"; // Query to create the 'Players' table

            const string createHighscoreTableQuery = @"
                CREATE TABLE Highscores (
                    Username NVARCHAR(50) NOT NULL,
                    Date DATETIME NOT NULL DEFAULT GETDATE(),
                    Score INT NOT NULL,
                    PRIMARY KEY (Username, Date),
                    FOREIGN KEY (Username) REFERENCES Players(Username)
                );"; // Query to create the 'Highscores' table

            try // Begin try block for exception handling
            {
                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection using the connection string
                {
                    await connection.OpenAsync(); // Open the SQL connection asynchronously
                    int tableCount = await connection.QuerySingleOrDefaultAsync<int>(checkExistsQuery); // Execute the query to check if the 'Players' table exists
                    if (tableCount == 0) // If the 'Players' table does not exist
                    {
                        await connection.ExecuteAsync(createPlayerTableQuery); // Create the 'Players' table
                        await connection.ExecuteAsync(createHighscoreTableQuery); // Create the 'Highscores' table
                    }
                }
            }
            catch (Exception ex) // Catch any exceptions that occur during initialization
            {
                Console.WriteLine($"Database initialization error: {ex.Message}"); // Log the exception message
            }
        }

        public async Task<string> CreateAccount(TcpClient client, string message) // Creates a new player account using provided credentials
        {
            string[] args = message.Split(":"); // Split the incoming message into arguments using ':' as the delimiter
            if (args.Length < 3) // Check if there are at least 3 parts (username, hash, salt)
            {
                return "Invalid message format."; // Return error message if format is invalid
            }

            string username = args[0]; // Extract the username from the message
            string hash = args[1]; // Extract the password hash from the message
            string salt = args[2]; // Extract the salt from the message

            string insertQuery = "INSERT INTO Players (Username, Hash, Salt) VALUES (@Username, @Hash, @Salt)"; // SQL query to insert a new player
            string checkQuery = "SELECT Username FROM Players WHERE Username = @Username"; // SQL query to check if the username already exists

            try // Begin try block for account creation
            {
                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously
                    var existingUser = await connection.QuerySingleOrDefaultAsync<string>(checkQuery, new { Username = username }); // Check if the username already exists
                    if (existingUser != null) // If an existing user is found
                    {
                        return "Player Exists"; // Return a message indicating the player already exists
                    }

                    int rowsAffected = await connection.ExecuteAsync(insertQuery, new { Username = username, Hash = hash, Salt = salt }); // Execute the insert query
                    Console.WriteLine($"{rowsAffected} row(s) inserted."); // Log the number of rows inserted
                    Server.LoggedInPlayers.Add(client, username); // Add the new player to the server's logged-in players collection
                    return "Success:1000"; // Return a success message with the default rating (1000)
                }
            }
            catch (Exception e) // Catch any exceptions during account creation
            {
                Console.WriteLine($"Error: {e.Message}"); // Log the exception message
                return $"Error: {e.Message}"; // Return an error message with the exception details
            }
        }

        public async Task<string> FetchSalt(string username) // Retrieves the salt for a given username
        {
            try // Begin try block for fetching the salt
            {
                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously
                    string query = "SELECT Salt FROM Players WHERE Username = @Username"; // SQL query to retrieve the salt for the specified username
                    var salt = await connection.QuerySingleOrDefaultAsync<string>(query, new { Username = username }); // Execute the query and get the salt
                    return salt ?? "Username"; // Return the salt if found; otherwise, return "Username"
                }
            }
            catch (Exception e) // Catch any exceptions
            {
                return e.Message; // Return the exception message
            }
        }

        public async Task<string> VerifyPlayer(TcpClient client, string message) // Verifies player credentials during login
        {
            string[] args = message.Split(':'); // Split the incoming message into arguments using ':' as the delimiter
            if (args.Length < 2) // Check if there are at least 2 parts (username and hash attempt)
            {
                return "Invalid message format."; // Return error message if format is invalid
            }

            string username = args[0]; // Extract the username from the message
            string hashAttempt = args[1]; // Extract the password hash attempt from the message

            try // Begin try block for verifying the player
            {
                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously
                    string query = "SELECT Hash, Rating FROM Players WHERE Username = @Username"; // SQL query to get the stored hash and rating for the username
                    var result = await connection.QuerySingleOrDefaultAsync(query, new { Username = username }); // Execute the query and retrieve the result
                    if (result != null) // If a matching player is found
                    {
                        string hash = result.Hash; // Get the stored hash from the result
                        int rating = result.Rating; // Get the stored rating from the result
                        if (hashAttempt == hash) // If the provided hash matches the stored hash
                        {
                            if (Server.LoggedInPlayers.Values.ToArray().Contains(username)) // Check if the player is already logged in
                            {
                                return "Logged In"; // Return message indicating the player is already logged in
                            }
                            Server.LoggedInPlayers.Add(client, username); // Add the player to the logged-in players collection
                            return $"Success{rating}"; // Return a success message with the player's rating
                        }
                        else // If the provided hash does not match
                        {
                            return "Password"; // Return a message indicating a password error
                        }
                    }
                    return "Database Error"; // Return an error message if no matching player is found
                }
            }
            catch (Exception e) // Catch any exceptions during verification
            {
                Console.WriteLine(e.Message); // Log the exception message
                return e.Message; // Return the exception message
            }
        }

        public async Task<int> CalculateEloChange(string winnerName, string loserName, bool tied = false) // Calculates the Elo rating change based on match result
        {
            try // Begin try block for Elo calculation
            {
                int winnerRating, loserRating; // Declare variables for winner and loser ratings

                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously
                    string query = "SELECT Rating FROM Players WHERE Username = @Username"; // SQL query to get a player's rating
                    winnerRating = await connection.QuerySingleOrDefaultAsync<int>(query, new { Username = winnerName }); // Retrieve the winner's rating
                    loserRating = await connection.QuerySingleOrDefaultAsync<int>(query, new { Username = loserName }); // Retrieve the loser's rating
                }

                double ratingDifference = winnerRating - loserRating; // Calculate the rating difference
                ratingDifference = Math.Max(-1000, Math.Min(1000, ratingDifference)); // Clamp the rating difference between -1000 and 1000

                int baseVal = tied ? 0 : 30; // Set base value: 0 if tied, otherwise 30
                double adjustment = baseVal - (ratingDifference / 100); // Calculate the adjustment based on rating difference
                adjustment = Math.Max(baseVal - 10, Math.Min(adjustment, baseVal + 10)); // Clamp the adjustment within a ±10 range of base value

                await UpdateRatings(winnerName, loserName, (int)Math.Round(adjustment)); // Update the players' ratings in the database
                return (int)Math.Round(adjustment); // Return the computed rating adjustment
            }
            catch (Exception ex) // Catch any exceptions during Elo calculation
            {
                Console.WriteLine($"Error in CalculateEloChange: {ex.Message}"); // Log the exception message
                return 0; // Return 0 adjustment on error
            }
        }

        public async Task UpdateRatings(string winnerName, string loserName, int adjustment) // Updates player ratings after a match
        {
            try // Begin try block for updating ratings
            {
                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously
                    string query = @"
                        UPDATE Players
                        SET Rating = 
                            CASE 
                                WHEN Username = @winnerName THEN Rating + @adjustment
                                WHEN Username = @loserName THEN Rating - @adjustment
                            END
                        WHERE Username IN (@winnerName, @loserName);"; // SQL query to update the ratings for both winner and loser
                    await connection.ExecuteAsync(query, new { winnerName, loserName, adjustment }); // Execute the update query with parameters
                }
            }
            catch (Exception ex) // Catch any exceptions during update
            {
                Console.WriteLine($"Error in UpdateRatings: {ex.Message}"); // Log the exception message
            }
        }

        public async Task UpdateHighscore(string username, int score) // Updates the highscore for a user if the new score is high enough
        {
            try // Begin try block for updating highscore
            {
                var currentHighScores = await FetchHighscoresAsync(username); // Fetch current highscores for the user

                using (var connection = new SqlConnection(connectionString)) // Create a new SQL connection
                {
                    await connection.OpenAsync(); // Open the connection asynchronously

                    if (currentHighScores.Count >= 10) // If the user already has 10 highscores recorded
                    {
                        var lowestScore = currentHighScores.OrderByDescending(h => h.Score).Last(); // Identify the lowest highscore
                        if (lowestScore.Score < score) // If the new score is higher than the lowest highscore
                        {
                            string removeQuery = @"
                                DELETE FROM Highscores 
                                WHERE Username = @Username AND Score = @LowestScore AND Date = @LowestScoreDate"; // SQL query to remove the lowest highscore
                            await connection.ExecuteAsync(removeQuery,
                                new { Username = username, LowestScore = lowestScore.Score, LowestScoreDate = lowestScore.Date }); // Execute the removal query
                        }
                        else // If the new score does not beat the lowest highscore
                        {
                            return; // Do not update the highscore
                        }
                    }

                    const string insertHighscoreQuery = @"
                        INSERT INTO Highscores (Username, Score) 
                        VALUES (@Username, @Score)"; // SQL query to insert a new highscore
                    await connection.ExecuteAsync(insertHighscoreQuery, new { Username = username, Score = score }); // Execute the insert query
                }
            }
            catch (Exception ex) // Catch any exceptions during highscore update
            {
                Console.WriteLine($"Error in UpdateHighscore: {ex.Message}"); // Log the exception message
            }
        }

        public List<Highscore> FetchHighscores(string username)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Retrieve highscores without sorting in SQL
                    const string fetchHighscoresQuery = @"
                        SELECT Score, CONVERT(NVARCHAR, Date) AS Date 
                        FROM Highscores 
                        WHERE Username = @Username";

                    var highscores = connection.Query<Highscore>(fetchHighscoresQuery, new { Username = username }).ToList();

                    // Apply merge sort to sort by Score in descending order
                    var sortedHighscores = MergeSort(highscores);

                    // Take only the top 10 after sorting
                    return sortedHighscores.Take(10).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FetchHighscores: {ex.Message}");
                return new List<Highscore>();
            }
        }

        // Modified FetchHighscoresAsync to use merge sort instead of SQL ORDER BY
        public async Task<List<Highscore>> FetchHighscoresAsync(string username)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Retrieve highscores without sorting in SQL
                    const string fetchHighscoresQuery = @"
                        SELECT Score, CONVERT(NVARCHAR, Date) AS Date 
                        FROM Highscores 
                        WHERE Username = @Username";

                    var highscores = (await connection.QueryAsync<Highscore>(fetchHighscoresQuery, new { Username = username })).ToList();

                    // Apply merge sort to sort by Score in descending order
                    var sortedHighscores = MergeSort(highscores);

                    // Take only the top 10 after sorting
                    return sortedHighscores.Take(10).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FetchHighscoresAsync: {ex.Message}");
                return new List<Highscore>();
            }
        }

        // Merge Sort implementation for Highscore objects (sorted by Score in descending order)
        private List<Highscore> MergeSort(List<Highscore> list)
        {
            // Base case: lists with 0 or 1 element are already sorted
            if (list.Count <= 1)
                return list;

            // Find the middle point to divide the list
            int middle = list.Count / 2;

            // Split the list into left and right sublists
            List<Highscore> left = new List<Highscore>();
            List<Highscore> right = new List<Highscore>();

            // Populate the sublists
            for (int i = 0; i < middle; i++)
                left.Add(list[i]);

            for (int i = middle; i < list.Count; i++)
                right.Add(list[i]);

            // Recursively sort both sublists
            left = MergeSort(left);
            right = MergeSort(right);

            // Merge the sorted sublists
            return Merge(left, right);
        }

        // Merge two sorted lists into one sorted list (descending order by Score)
        private List<Highscore> Merge(List<Highscore> left, List<Highscore> right)
        {
            List<Highscore> result = new List<Highscore>();
            int leftIndex = 0, rightIndex = 0;

            // Compare elements from both lists and add them in descending order
            while (leftIndex < left.Count && rightIndex < right.Count)
            {
                // For descending order, we take the higher score first
                if (left[leftIndex].Score >= right[rightIndex].Score)
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

            // Add remaining elements from either list
            while (leftIndex < left.Count)
            {
                result.Add(left[leftIndex]);
                leftIndex++;
            }

            while (rightIndex < right.Count)
            {
                result.Add(right[rightIndex]);
                rightIndex++;
            }

            return result;
        }
    }
}
