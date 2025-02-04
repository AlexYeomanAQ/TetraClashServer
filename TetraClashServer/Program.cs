using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TetraClashServer
{
    public class Program
    {
        public static void Main()
        {
            Server server = new Server();
            Task.Run(() => server.Main()).Wait();
        }
    }
}
