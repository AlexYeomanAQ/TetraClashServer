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
        static void TryMatchPlayers()
        {
            if (Queue.Count >= 2)
            {
                List<Player> matchPlayers = Queue.GetRange(0, 2);
                Queue.RemoveRange(0, 2);


                Console.WriteLine("Match created:");
                foreach (Player player in matchPlayers)
                {
                    Console.WriteLine($" - {player.Name}");
                    NotifyPlayer(player, "Match found vs");
                }
            }
        }

        static void NotifyPlayer(TcpClient client, string message)
        {
            NetworkStream stream = player.Client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);
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