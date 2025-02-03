using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TetraClashServer
{
    class Server
    {
        static Timer? queueTimer;

        static async Task Main()
        {
            bool dbInit = await Database.Initialize();

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
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(client));
            }
        }

        static async Task HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0) return; // Client disconnected

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received: {message}");

                string response = await HandleResponse(client, message);

                if (response == "Queue") return;

                await SendResponse(client, response); // Async send
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                client.Close(); // Close the client connection after processing
            }
        }

        public static async Task<string> HandleResponse(TcpClient client, string message)
        {
            string cropMessage;
            string response = "";

            if (message.StartsWith("search"))
            {
                cropMessage = message.Substring(6);
                MatchMaking.EnQueue(client, cropMessage);
                response = "Queue";

            }
            else if (message.StartsWith("cancel"))
            {
                cropMessage = message.Substring(6);
                MatchMaking.DeQueue(cropMessage);
                response = "Success";
            }
            else if (message.StartsWith("create"))
            {
                cropMessage = message.Substring(6);
                response = await Database.CreateAccount(cropMessage); // Use async DB method
            }
            else if (message.StartsWith("login"))
            {
                cropMessage = message.Substring(5);
                response = await Database.VerifyPlayer(cropMessage); // Use async DB method
            }
            else if (message.StartsWith("salt"))
            {
                cropMessage = message.Substring(4);
                response = await Database.FetchSalt(cropMessage); // Use async DB method
            }
            else
            {
                response = "Unknown Request";
            }

            return response;
        }

        public static async Task SendResponse(TcpClient client, string response)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(buffer, 0, buffer.Length); // Async send
        }

        static void TryPlayers(object state)
        {
            MatchMaking.TryMatchPlayers();
        }
    }
}
