using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

namespace TetraClashServer
{
    class Server
    {
        static MatchMaking matchmaking = new MatchMaking();
        static void Main()
        {
            bool dbInit = Database.Initialize();

            if (!dbInit)
            {
                Environment.Exit(0);
            }

            TcpListener server = new TcpListener(IPAddress.Any, 12345);
            server.Start();
            Console.WriteLine("Server Online");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {message}");
                HandleResponse(client, message);
            }
        }

        static void HandleResponse(TcpClient client, string message)
        {
            string cropMessage;
            string query = "";

            if (message.StartsWith("search"))
            {
                cropMessage = message.Substring(6);
                
                matchmaking.EnQueue(client, cropMessage);
            }
            else if (message.StartsWith("create"))
            {
                cropMessage = message.Substring(6);

                query = Database.CreateAccount(cropMessage);
            }
            else if (message.StartsWith("login"))
            {
                cropMessage = message.Substring(5);

                query = Database.VerifyPlayer(cropMessage);
            }
            else if (message.StartsWith("salt"))
            {
                cropMessage = message.Substring(4);

                query = Database.fetchSalt(cropMessage);
            }
            else
            {
                query = "Unknown Request";
            }
            sendResponse(client, query);
        }

        public static void sendResponse(TcpClient client, string response)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(response);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
