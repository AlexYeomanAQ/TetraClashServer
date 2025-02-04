using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TetraClashServer
{
    public class Server
    {
        public MatchMaking matchmaking;
        public async Task Main()
        {
            matchmaking = new MatchMaking(this);
            bool dbInit = await Database.Initialize();

            if (!dbInit)
            {
                Environment.Exit(0);
            }

            _ = StartMatchmakingLoop();

            TcpListener server = new TcpListener(IPAddress.Any, 12345);
            server.Start();
            Console.WriteLine("Server Online");

            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClient(client));
            }
        }

        public async Task HandleClient(TcpClient client)
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

        public async Task<string> HandleResponse(TcpClient client, string message)
        {
            string cropMessage;
            string response = "";

            if (message.StartsWith("search"))
            {
                cropMessage = message.Substring(7);
                matchmaking.EnQueue(client, cropMessage);
                response = "Queue";

            }
            else if (message.StartsWith("cancel"))
            {
                cropMessage = message.Substring(7);
                await Console.Out.WriteLineAsync(cropMessage);
                matchmaking.DeQueue(cropMessage);
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
            else if (message.StartsWith("grid"))
            {
                cropMessage = message.Substring(4);
                matchmaking.HandleMatchData(client, cropMessage);
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

        public async Task StartMatchmakingLoop()
        {
            while (true)
            {
                await matchmaking.TryMatchPlayers();
                await Task.Delay(500);
            }
        }
    }
}
