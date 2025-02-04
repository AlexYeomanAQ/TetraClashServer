using System.Net.Sockets;

namespace TetraClashServer
{
    public class Player
    {
        public TcpClient Client;
        public string Name;

        public Player(TcpClient client, string name)
        {
            Client = client;
            Name = name;
        }
    }
}
