using System.Collections.Concurrent; // Provides thread-safe collections.
using System.Net.Sockets;              // Provides classes for working with sockets.

namespace TetraClashServer
{
    // The Matchmaking class manages player matchmaking by pairing players waiting for a game match.
    public class Matchmaking
    {
        // Reference to the main server object to access shared server data.
        private Server server;

        // A thread-safe queue that holds players waiting to be matched.
        private ConcurrentQueue<TcpClient> _waitingPlayers = new ConcurrentQueue<TcpClient>();

        // A thread-safe dictionary mapping each client to their current match.
        public ConcurrentDictionary<TcpClient, Match> _clientMatches = new ConcurrentDictionary<TcpClient, Match>();

        // Constructor initializing Matchmaking with a reference to the server.
        public Matchmaking(Server server)
        {
            this.server = server;
        }

        // HandleMatchmaking enqueues a client and attempts to find a match when two or more players are waiting.
        public async Task HandleMatchmaking(TcpClient client)
        {
            // Add the client to the waiting players queue.
            _waitingPlayers.Enqueue(client);

            // When at least two players are waiting, dequeue two players to create a match.
            if (_waitingPlayers.Count >= 2)
            {
                if (_waitingPlayers.TryDequeue(out TcpClient player1) &&
                    _waitingPlayers.TryDequeue(out TcpClient player2))
                {
                    // Generate a unique match ID using the current Unix timestamp.
                    string matchID = ((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();

                    // Log that a match has been created.
                    Console.WriteLine($"Match created: {matchID}");

                    // Create a match object with the pair of players.
                    Match match = new Match { MatchID = matchID, Player1 = player1, Player2 = player2 };

                    // Associate both players with the match in the dictionary.
                    _clientMatches[player1] = match;
                    _clientMatches[player2] = match;

                    // Prepare the response message for the matched players.
                    string response = $"MATCH_FOUND:{matchID}:";

                    // Send the match response to both players, including the opponent's username.
                    await Client.SendMessage(player1, response + server.LoggedInPlayers[player2]);
                    await Client.SendMessage(player2, response + server.LoggedInPlayers[player1]);
                }
            }
        }

        // RemovePlayer is called to remove a player from the matchmaking queue when they cancel searching.
        public string RemovePlayer(TcpClient client)
        {
            // Create a temporary queue to hold players who are still searching.
            ConcurrentQueue<TcpClient> tempQueue = new ConcurrentQueue<TcpClient>();

            // Rebuild the queue while excluding the canceled client.
            try
            {
                while (_waitingPlayers.TryDequeue(out TcpClient waitingClient))
                {
                    if (waitingClient != client)
                    {
                        // Enqueue players that are not the one being removed.
                        tempQueue.Enqueue(waitingClient);
                    }
                    else
                    {
                        // Log the removal of the player from the queue.
                        Console.WriteLine("Player removed from matchmaking queue.");
                    }
                }

                // Replace the original queue with the filtered queue.
                _waitingPlayers = tempQueue;
                return "Success";
            }
            catch (Exception e)
            {
                // Return the exception message if any error occurs.
                return e.Message;
            }
        }
    }
}