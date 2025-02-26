using ServerTest2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TetraClashServer
{
    public class Matchmaking
    {
        private ConcurrentQueue<TcpClient> _waitingPlayers = new ConcurrentQueue<TcpClient>();
        public ConcurrentDictionary<TcpClient, Match> _clientMatches = new ConcurrentDictionary<TcpClient, Match>();

        public async Task HandleMatchmaking(TcpClient client)
        {
            _waitingPlayers.Enqueue(client);

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

        public string RemovePlayer(TcpClient client)
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
    }
}
