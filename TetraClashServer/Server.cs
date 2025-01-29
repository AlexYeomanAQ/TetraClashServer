using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace TetraClashServer
{
    class Server
    {
        static Timer queueTimer;
        static void Main()
        {
            bool dbInit = Database.Initialize();

            if (!dbInit)
            {
                Environment.Exit(0);
            }

            queueTimer = new Timer(TryPlayers, null, 0, 500);

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
            string response = "";

            if (message.StartsWith("search"))
            {
                cropMessage = message.Substring(6);

                MatchMaking.EnQueue(client, cropMessage);
            }
            else if (message.StartsWith("cancel"))
            {
                cropMessage = message.Substring(6);
                
                MatchMaking.DeQueue(client, cropMessage);
            }
            else if (message.StartsWith("create"))
            {
                cropMessage = message.Substring(6);

                response = Database.CreateAccount(cropMessage);
            }
            else if (message.StartsWith("login"))
            {
                cropMessage = message.Substring(5);

                response = Database.VerifyPlayer(cropMessage);
            }
            else if (message.StartsWith("salt"))
            {
                cropMessage = message.Substring(4);

                response = Database.FetchSalt(cropMessage);
            }
            //else if (message.StartsWith("match"))
            //{
            //    cropMessage = message.Substring(5);

            //    response = MatchMaking.Process
            //}
            else
            {
                response = "Unknown Request";
            }

            sendResponse(client, response);
        }

        public static void sendResponse(TcpClient client, string response)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(response);
            stream.Write(buffer, 0, buffer.Length);
        }

        static void TryPlayers(object state)
        {
            MatchMaking.TryMatchPlayers();
        }
    }
}
