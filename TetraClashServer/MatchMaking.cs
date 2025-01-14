using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace TetraClashServer
{
    class MatchMaking
    {
        private static List<Player> Queue = new List<Player>();

        public MatchMaking() { }
        public void TryMatchPlayers()
        {
            if (Queue.Count >= 2)
            {
                List<Player> matchPlayers = Queue.GetRange(0, 2);
                Queue.RemoveRange(0, 2);


                Console.WriteLine("Match created:");
                for (int i = 0; i < matchPlayers.Count; i++)
                {
                    Console.WriteLine($" - {matchPlayers[i].Name}");
                    Server.sendResponse(matchPlayers[i].Client, $"Match found vs {matchPlayers[(i + 1) % 2]}");
                }
            }
        }
        public void EnQueue(TcpClient client, string message)
        {
            string username = message.Substring(12);
            Player player = new Player(client, message);
            Queue.Add(player);
            Console.WriteLine($"{username} added to the queue.");
        }
    }
}