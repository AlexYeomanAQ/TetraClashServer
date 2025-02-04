using System.Net.Sockets;

namespace TetraClashServer
{
    public class MatchMaking
    {
        private List<Player> Queue;
        public Dictionary<int, Match> ActiveMatches = new Dictionary<int, Match>();

        private Server server;
        public MatchMaking(Server _server)
        {
            server = _server;
            Queue = new List<Player>();
        }
        public async Task TryMatchPlayers()
        {
            foreach (var player in Queue)
            {
                Console.WriteLine(player.Name);
            }
            if (Queue.Count >= 2)
            {
                List<Player> matchPlayers = Queue.GetRange(0, 2);
                Queue.RemoveRange(0, 2);

                int matchID = CreateMatch(matchPlayers[0], matchPlayers[1]);
                Console.WriteLine($"MatchID = {matchID}");

                foreach (var player in matchPlayers)
                {
                    Console.WriteLine(player.Name);
                    await Server.SendResponse(player.Client, $"found:{matchID}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
                }
            }
        }
        public void EnQueue(TcpClient client, string username)
        {
            Player player = new Player(client, username);
            Queue.Add(player);
        }

        public void DeQueue(string username)
        {
            for (int i = 0; i < Queue.Count; i++)
            {
                if (Queue[i].Name == username)
                {
                    Queue.Remove(Queue[i]);
                }
            }
        }

        public int CreateMatch(Player player1, Player player2)
        {
            var match = new Match(player1, player2);
            int matchID = ActiveMatches.Count() + 1;
            ActiveMatches.Add(matchID, match);
            return matchID;
        }

        public async void HandleMatchData(TcpClient client, string message)
        {
            string[] args = message.Split(':');
            int matchID = int.Parse(args[0]);
            string gridJson = args[1];
            if (ActiveMatches.ContainsKey(matchID))
            {
                await ActiveMatches[matchID].HandleJson(client, gridJson);
            }
            else
            {
                Console.WriteLine($"No active match found for ID: {matchID}");
            }
        }
    }
}