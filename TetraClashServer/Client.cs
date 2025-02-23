using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ServerTest2
{
    public static class Client
    {
        public async static Task SendMessage(TcpClient client, string message)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] responseBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                Console.WriteLine($"Sent {message} to {client.GetHashCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending match found: " + ex.Message);
            }
        }
    }
}
