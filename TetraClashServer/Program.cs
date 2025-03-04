using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TetraClashServer;

namespace TetraClashServer
{
    public class Match
    {
        public string MatchID { get; set; }
        public TcpClient Player1 { get; set; }
        public TcpClient Player2 { get; set; }

        public int? Player1Score { get; set; }
        public int? Player2Score { get; set; }
    }
    public class Server
    {
        private TcpListener _listener;

        private Database database;
        private Matchmaking matchmaking;

        public Dictionary<TcpClient, string> LoggedInPlayers = new Dictionary<TcpClient, string>();
        public void Start()
        {
            try
            {
                database = new Database(this);
            }
            catch
            {
                Console.WriteLine("Failed to initialize, press enter to exit.");
                Console.ReadLine();
                Environment.Exit(0);
            }
            matchmaking = new Matchmaking(this);
            // Listen on any IP address on port 5000
            _listener = new TcpListener(IPAddress.Any, 5000);
            _listener.Start(); //Starts the process to listen to incoming messages/requests
            Console.WriteLine("Server started on port 5000...");

            AcceptClientsAsync();
        }

        private async void AcceptClientsAsync()
        {
            while (true)
            {
                // Accept a new client connection
                TcpClient client = await _listener.AcceptTcpClientAsync();
                ProcessClientAsync(client);
            }
        }

        private async void ProcessClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            // Continue to process messages while the client is connected
            while (client.Connected)
            {
                int bytesRead = 0;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error reading from client: " + ex.Message);
                    break;
                }

                // If zero bytes are read, the client disconnected.
                if (bytesRead == 0)
                {
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                string args;
                string response = "";

                if (message.StartsWith("search"))
                {
                    await matchmaking.HandleMatchmaking(client);
                }
                else if (message.StartsWith("cancel"))
                {
                    response = matchmaking.RemovePlayer(client);
                }
                else if (message.StartsWith("create"))
                {
                    args = message.Substring(6);
                    response = await database.CreateAccount(client, args);
                }
                else if (message.StartsWith("login"))
                {
                    args = message.Substring(5);
                    response = await database.VerifyPlayer(client, args);
                }
                else if (message.StartsWith("salt"))
                {
                    args = message.Substring(4);
                    response = await database.FetchSalt(args);
                }
                else if (message.StartsWith("match"))
                {
                    args = message.Substring(6);
                    HandleMatchUpdate(client, args);
                }
                else if (message.StartsWith("lose"))
                {
                    HandleMatchEnd(client);
                }
                else if (message.StartsWith("time"))
                {
                    HandleMatchEndTimer(client, message.Substring(4));
                }
                else
                {
                    response = "Unknown Message";
                }
                if (response != "")
                {
                    await Client.SendMessage(client, response);
                }
            }

            try
            {
                LoggedInPlayers.Remove(client);
            }
            catch { }
            matchmaking._clientMatches.TryRemove(client, out _);
            client.Close();
        }

        private async void HandleMatchUpdate(TcpClient sender, string gridData)
        {
            if (matchmaking._clientMatches.TryGetValue(sender, out Match match))
            {
                // Determine the other player in the match.
                TcpClient receiver = (match.Player1 == sender) ? match.Player2 : match.Player1;
                await SendGridUpdate(receiver, gridData);
            }
            else
            {
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        private async void HandleMatchEnd(TcpClient sender)
        {

            if (matchmaking._clientMatches.TryRemove(sender, out Match match))
            {
                // Determine the other player in the match.
                TcpClient receiver = (match.Player1 == sender) ? match.Player2 : match.Player1;
                matchmaking._clientMatches.Remove(receiver, out _);
                int adjustment = await database.CalculateEloChange(LoggedInPlayers[receiver], LoggedInPlayers[sender]);
                await Client.SendMessage(receiver, $"MATCH_WIN:{adjustment}");
                await Client.SendMessage(sender, $"MATCH_LOSE:{adjustment}");
            }
            else
            {
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        private async void HandleMatchEndTimer(TcpClient sender, string score)
        {
            if (matchmaking._clientMatches.TryGetValue(sender, out Match match))
            {
                int playerScore = int.Parse(score.Trim());

                // Update the appropriate player's score.
                if (match.Player1 == sender)
                {
                    match.Player1Score = playerScore;
                }
                else if (match.Player2 == sender)
                {
                    match.Player2Score = playerScore;
                }

                if (match.Player1Score != null && match.Player2Score != null)
                {
                    TcpClient? winner = null;
                    TcpClient? loser = null;

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
                        matchmaking._clientMatches.TryRemove(winner, out _);
                        matchmaking._clientMatches.TryRemove(loser, out _);
                        int adjustment = await database.CalculateEloChange(LoggedInPlayers[winner], LoggedInPlayers[loser]);
                        if (match.Player1Score == match.Player2Score)
                        {
                            if (adjustment > 0)
                            {
                                await Client.SendMessage(loser, $"MATCH_TIE_WIN:{adjustment}");
                                await Client.SendMessage(winner, $"MATCH_TIE_LOSE:{adjustment}");
                            }
                            else
                            {
                                await Client.SendMessage(winner, $"MATCH_TIE_WIN:{adjustment}");
                                await Client.SendMessage(loser, $"MATCH_TIE_LOSE:{adjustment}");
                            }
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
                    matchmaking._clientMatches[sender] = match;
                }
            }
            else
            {
                Console.WriteLine("Sender not in a match, ignoring match update.");
            }
        }

        private async Task SendGridUpdate(TcpClient client, string gridData)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string response = $"GRID_UPDATE:{gridData}";
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                Console.WriteLine("Forwarded grid update to client.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending grid update: " + ex.Message);
            }
        }
    }
    class Program
    {
        static void Main()
        {
            Server server = new Server();
            server.Start();

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
