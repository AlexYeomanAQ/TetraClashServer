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
            if (message.StartsWith("search"))
            {
                cropMessage = message.Substring(6);
                
                matchmaking.EnQueue(client, cropMessage);
            }
            else if (message.StartsWith("create"))
            {
                cropMessage = message.Substring(6);

                Database.CreateAccount(client, cropMessage);
            }
            else if (message.StartsWith("login"))
            {
                cropMessage = message.Substring(5);

                Database.VerifyPlayer(client, cropMessage);
            }
        }

        static void sendMessage()
    }
}
