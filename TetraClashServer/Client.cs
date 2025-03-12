using System.Net.Sockets; // Import networking classes, including TcpClient  
using System.Text; // Import text encoding utilities  

namespace TetraClashServer // Define the TetraClashServer namespace  
{
    public static class Client // Static class for client-related operations  
    {
        public async static Task SendMessage(TcpClient client, string message) // Asynchronously sends a message using the provided TcpClient  
        {
            try // Begin try block to catch any exceptions during the send process  
            {
                NetworkStream stream = client.GetStream(); // Retrieve the network stream from the TcpClient  
                byte[] responseBytes = Encoding.UTF8.GetBytes(message); // Convert the message string into a UTF8-encoded byte array  
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length); // Asynchronously write the byte array to the network stream  
                Console.WriteLine($"Sent {message} to {client.GetHashCode}"); // Log the sent message and the client's hash code  
            }
            catch (Exception ex) // Catch any exceptions that occur during message sending  
            {
                Console.WriteLine("Error sending match found: " + ex.Message); // Log the error message if an exception occurs  
            }
        }
    }
}
