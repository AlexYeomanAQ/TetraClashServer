using ServerTest2;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DedicatedGameServer
{
    public class Match
    {
        public string MatchID { get; set; }
        public TcpClient Player1 { get; set; }
        public TcpClient Player2 { get; set; }
    }
    public class Server
    {
        private TcpListener _listener;
        // Thread-safe queue to hold waiting players
        private ConcurrentQueue<TcpClient> _waitingPlayers = new ConcurrentQueue<TcpClient>();
        private ConcurrentDictionary<TcpClient, Match> _clientMatches = new ConcurrentDictionary<TcpClient, Match>();

        public void Start()
        {
            if (!Database.Initialize())
            {
                Environment.Exit(0);
            }
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
                Console.WriteLine("Client connected.");
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
                    HandleMatchmaking(client);
                }
                else if (message.StartsWith("cancel"))
                {
                    response = RemovePlayer(client);
                }
                else if (message.StartsWith("create"))
                {
                    args = message.Substring(6);
                    response = await Database.CreateAccount(args);
                }
                else if (message.StartsWith("login"))
                {
                    args = message.Substring(5);
                    response = await Database.VerifyPlayer(args);
                }
                else if (message.StartsWith("salt"))
                {
                    args = message.Substring(4);
                    response = await Database.FetchSalt(args);
                }
                else if (message.StartsWith("match:", StringComparison.InvariantCultureIgnoreCase))
                {
                    HandleMatchUpdate(client, message.Substring("match:".Length));
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

            _clientMatches.TryRemove(client, out _);
            client.Close();
        }

        private async Task HandleMatchmaking(TcpClient client)
        {
            _waitingPlayers.Enqueue(client);
            Console.WriteLine("Player added to matchmaking queue.");

            if (_waitingPlayers.Count >= 2)
            {
                if (_waitingPlayers.TryDequeue(out TcpClient player1) &&
                    _waitingPlayers.TryDequeue(out TcpClient player2))
                {
                    string matchID = ((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
                    Console.WriteLine($"Match created: {matchID}");

                    Match match = new Match { MatchID = matchID, Player1 = player1, Player2 = player2 };
                    _clientMatches[player1] = match;
                    _clientMatches[player2] = match;

                    string response = $"MATCH_FOUND:{matchID}";
                    await Client.SendMessage(player1, response);
                    await Client.SendMessage(player2, response);
                }
            }
        }

        private string RemovePlayer(TcpClient client)
        {
            // Create a temporary queue to hold players who are still searching.
            ConcurrentQueue<TcpClient> tempQueue = new ConcurrentQueue<TcpClient>();

            // Rebuild the queue without the canceled client.
            try
            {
                while (_waitingPlayers.TryDequeue(out TcpClient waitingClient))
                {
                    if (waitingClient != client)
                    {
                        tempQueue.Enqueue(waitingClient);
                    }
                    else
                    {
                        Console.WriteLine("Player removed from matchmaking queue.");
                    }
                }

                // Replace the original queue with the filtered one.
                _waitingPlayers = tempQueue;
                return "Success";
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private async void HandleMatchUpdate(TcpClient sender, string gridData)
        {
            if (_clientMatches.TryGetValue(sender, out Match match))
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
        static void Main(string[] args)
        {
            Server server = new Server();
            server.Start();

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
