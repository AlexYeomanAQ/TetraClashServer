using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
