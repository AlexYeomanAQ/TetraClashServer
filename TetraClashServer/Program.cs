using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TetraClashServer
{
    // The Server class handles the overall server operations such as starting the listener,
    // accepting clients, processing messages, and coordinating with matchmaking and database operations.
    public class Server
    {
        private TcpListener _listener; // Listens for incoming TCP connections

        private Database database;       // Handles database operations (e.g., account creation, verification)
        private Matchmaking matchmaking; // Handles matchmaking between clients

        // Dictionary to keep track of logged-in players. Maps TcpClient objects to their associated username.
        public Dictionary<TcpClient, string> LoggedInPlayers = new Dictionary<TcpClient, string>();

        // Start() initializes the server components and begins listening for incoming connections.
        public void Start()
        {
            try
            {
                // Initialize the database connection.
                database = new Database(this);
            }
            catch
            {
                // If the database initialization fails, notify and exit.
                Console.WriteLine("Failed to initialize, press enter to exit.");
                Console.ReadLine();
                Environment.Exit(0);
            }
            // Initialize the matchmaking system.
            matchmaking = new Matchmaking(this);

            // Listen on any IP address on port 5000.
            _listener = new TcpListener(IPAddress.Any, 5000);
            _listener.Start(); // Start listening for client connections.
            Console.WriteLine("Server started on port 5000...");

            // Begin accepting incoming client connections asynchronously.
            AcceptClientsAsync();
        }

        // AcceptClientsAsync continuously accepts new client connections.
        private async void AcceptClientsAsync()
        {
            while (true)
            {
                // Accept a new client connection asynchronously.
                TcpClient client = await _listener.AcceptTcpClientAsync();
                // Process the connected client's requests.
                ProcessClientAsync(client);
            }
        }

        // ProcessClientAsync handles the communication with a connected client.
        // It reads incoming messages, processes commands, and sends responses.
        private async void ProcessClientAsync(TcpClient client)
        {
            // Get the network stream for reading and writing data.
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            // Keep processing messages while the client is connected.
            while (client.Connected)
            {
                int bytesRead = 0;
                try
                {
                    // Read data from the client.
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    // Log any error encountered during reading.
                    Console.WriteLine("Error reading from client: " + ex.Message);
                    break;
                }

                // If the client sends zero bytes, then the client has disconnected.
                if (bytesRead == 0)
                {
                    break;
                }

                // Convert the incoming bytes into a string message.
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                string args;
                string response = "";

                // Process various commands based on the message content.
                if (message.StartsWith("search"))
                {
                    // Initiate matchmaking for the client.
                    await matchmaking.HandleMatchmaking(client);
                }
                else if (message.StartsWith("cancel"))
                {
                    // Cancel matchmaking for the client.
                    response = matchmaking.RemovePlayer(client);
                }
                else if (message.StartsWith("create"))
                {
                    // Create a new account, parsing the argument from the message.
                    args = message.Substring(6);
                    response = await database.CreateAccount(client, args);
                }
                else if (message.StartsWith("login"))
                {
                    // Log in the player using the credentials provided in the message.
                    args = message.Substring(5);
                    response = await database.VerifyPlayer(client, args);
                }
                else if (message.StartsWith("salt"))
                {
                    // Retrieve the salt required for password verification.
                    args = message.Substring(4);
                    response = await database.FetchSalt(args);
                }
                else if (message.StartsWith("match"))
                {
                    // Handle a match update by forwarding grid data to the opponent.
                    args = message.Substring(6);
                    HandleMatchUpdate(client, args);
                }
                else if (message.StartsWith("lose"))
                {
                    // End the match for the client and update the highscore based on the passed score value.
                    args = message.Substring(4);
                    await HandleMatchEnd(client);
                    await database.UpdateHighscore(LoggedInPlayers[client], int.Parse(args));
                }
                else if (message.StartsWith("time"))
                {
                    // Process a timer event to handle the match end with score details.
                    HandleMatchEndTimer(client, message.Substring(4));
                }
                else if (message.StartsWith("score"))
                {
                    // Update the highscore with a direct score update, provided the client is logged in.
                    args = message.Substring(5);
                    if (LoggedInPlayers.ContainsKey(client))
                    {
                        await database.UpdateHighscore(LoggedInPlayers[client], int.Parse(args));
                    }
                }
                else if (message.StartsWith("highscores"))
                {
                    // Log a debug message then send highscore data serialized as JSON to the client.
                    args = message.Substring(10);
                    await Console.Out.WriteLineAsync(args);
                    response = JsonSerializer.Serialize(database.FetchHighscores(args));
                }
                else
                {
                    // If no recognized command matches, return an error message.
                    response = "Unknown Message";
                }

                // If a response has been generated, send it back to the client.
                if (response != "")
                {
                    await Client.SendMessage(client, response);
                }
            }
            // Once client is disconnected, make sure to remove them from any ongoing match and from logged-in players.
            try
            {
                if (matchmaking._clientMatches.ContainsKey(client))
                {
                    await HandleMatchEnd(client);
                }
                LoggedInPlayers.Remove(client);
            }
            catch { }

            // Close the client connection.
            client.Close();
        }

        // HandleMatchUpdate forwards grid data from one player to their opponent during a match.
        private async void HandleMatchUpdate(TcpClient sender, string gridData)
        {
            if (matchmaking._clientMatches.TryGetValue(sender, out Match match))
            {
                // Identify the other player in the match.
                TcpClient receiver = (match.Player1 == sender) ? match.Player2 : match.Player1;
                // Send the grid update to the opposing client.
                await SendGridUpdate(receiver, gridData);
            }
            else
            {
                // If the sender is not found in any match, log a message.
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        // HandleMatchEnd performs match termination procedures when a player loses.
        // It calculates Elo rating changes, notifies both players, and removes them from the matchmaking pool.
        private async Task HandleMatchEnd(TcpClient sender)
        {
            if (matchmaking._clientMatches.TryRemove(sender, out Match match))
            {
                // Determine the opponent based on the sender.
                TcpClient receiver = (match.Player1 == sender) ? match.Player2 : match.Player1;
                // Remove the opponent from the matchmaking if their match exists.
                matchmaking._clientMatches.Remove(receiver, out _);
                // Calculate the Elo rating change after the match.
                int adjustment = await database.CalculateEloChange(LoggedInPlayers[receiver], LoggedInPlayers[sender]);
                // Notify both players about the results.
                await Client.SendMessage(receiver, $"MATCH_WIN:{adjustment}");
                await Client.SendMessage(sender, $"MATCH_LOSE:{adjustment}");
            }
            else
            {
                // Log message if the sender was not in a match.
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        // HandleMatchEndTimer manages the logic for ending a match with a time-based score update.
        private async void HandleMatchEndTimer(TcpClient sender, string score)
        {
            if (matchmaking._clientMatches.TryGetValue(sender, out Match match))
            {
                // Parse the score from the message.
                int playerScore = int.Parse(score.Trim());

                // Update the score in the match for the corresponding player.
                if (match.Player1 == sender)
                {
                    match.Player1Score = playerScore;
                }
                else if (match.Player2 == sender)
                {
                    match.Player2Score = playerScore;
                }

                // If both players have reported their scores, proceed with determining the match outcome.
                if (match.Player1Score != null && match.Player2Score != null)
                {
                    TcpClient? winner = null;
                    TcpClient? loser = null;

                    // Determine the winner based on the higher score.
                    if (match.Player1Score >= match.Player2Score)
                    {
                        winner = match.Player1;
                        loser = match.Player2;
                    }
                    else if (match.Player2Score > match.Player1Score)
                    {
                        winner = match.Player2;
                        loser = match.Player1;
                    }

                    if (winner != null && loser != null)
                    {
                        // Remove both players from matchmaking.
                        matchmaking._clientMatches.TryRemove(winner, out _);
                        matchmaking._clientMatches.TryRemove(loser, out _);
                        // Calculate Elo rating adjustments.
                        int adjustment = await database.CalculateEloChange(LoggedInPlayers[winner], LoggedInPlayers[loser]);
                        // Update highscore information for both players.
                        await database.UpdateHighscore(LoggedInPlayers[match.Player1], match.Player1Score);
                        await database.UpdateHighscore(LoggedInPlayers[match.Player2], match.Player2Score);
                        // In the event of a tie, send a tie message; otherwise, announce winner and loser.
                        if (match.Player1Score == match.Player2Score)
                        {
                            await Client.SendMessage(adjustment > 0 ? loser : winner, $"MATCH_TIE_WIN:{adjustment}");
                            await Client.SendMessage(adjustment > 0 ? winner : loser, $"MATCH_TIE_LOSE:{adjustment}");
                        }
                        else
                        {
                            await Client.SendMessage(winner, $"MATCH_WIN:{adjustment}");
                            await Client.SendMessage(loser, $"MATCH_LOSE:{adjustment}");
                        }
                    }
                }
                else
                {
                    // If only one player's score is updated, keep the match record for later updates.
                    matchmaking._clientMatches[sender] = match;
                }
            }
            else
            {
                // If the sender is not in a match, log the incident.
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        // SendGridUpdate is responsible for transmitting grid updates to a particular client.
        private async Task SendGridUpdate(TcpClient client, string gridData)
        {
            try
            {
                // Retrieve the network stream to send data.
                NetworkStream stream = client.GetStream();
                // Format the grid update message.
                string response = $"GRID_UPDATE:{gridData}";
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                // Send the grid update to the client asynchronously.
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                Console.WriteLine("Forwarded grid update to client.");
            }
            catch (Exception ex)
            {
                // Log an error if the grid update transmission fails.
                Console.WriteLine("Error sending grid update: " + ex.Message);
            }
        }
    }

    // The Program class contains the Main method which is the entry point of the application.
    class Program
    {
        // Main() initializes and starts the server, then waits for user input to exit.
        static void Main()
        {
            // Create a new server instance.
            Server server = new Server();
            // Start the server operations.
            server.Start();

            Console.WriteLine("Press ENTER to exit.");
            // Prevent the application from exiting immediately.
            Console.ReadLine();
        }
    }
}