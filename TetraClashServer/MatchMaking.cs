using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Xml.Linq;

namespace TetraClashServer
{
    public static class MatchMaking
    {
        private static List<Player> Queue = new List<Player>();
        private static Dictionary<int, Match> ActiveMatches = new Dictionary<int, Match>();
        public static void TryMatchPlayers()
        {
            if (Queue.Count >= 2)
            {
                List<Player> matchPlayers = Queue.GetRange(0, 2);
                Queue.RemoveRange(0, 2);

                int matchID = CreateMatch(matchPlayers[0], matchPlayers[1]);

                for (int i = 0; i < matchPlayers.Count; i++)
                {
                    Server.sendResponse(matchPlayers[i].Client, $"found:{matchID}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
                }
            }
        }
        public static void EnQueue(TcpClient client, string username)
        {
            Player player = new Player(client, username);
            Queue.Add(player);
        }

        public static void DeQueue(TcpClient client, string username)
        {
            for (int i = 0; i < Queue.Count; i++)
            {
                if (Queue[i].Name == username)
                {
                    Queue.RemoveAt(i);
                }
            }
        }

        public static int CreateMatch(Player player1, Player player2)
        {
            var match = new Match(player1, player2);
            int matchID = ActiveMatches.Count() + 1;
            ActiveMatches.Add(matchID, match);

            _ = Task.Run(match.StartAsync);
            return matchID;
        }

        public static void HandleMatchData(int matchID, TcpClient client, string message)
        {
            if (ActiveMatches.ContainsKey(matchID))
            {
                ActiveMatches[matchID].HandleMessage(client, message);
            }
            else
            {
                Console.WriteLine($"No active match found for ID: {matchID}");
            }
        }
    }
}