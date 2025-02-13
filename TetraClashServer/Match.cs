using System.Net.Sockets;
using TetraClashServer;

public class Match
{
    private int ID { get; }
    private Player Player1 { get; }
    private Player Player2 { get; }

    public Match(int id, Player player1, Player player2)
    {
        ID = id;
        Player1 = player1;
        Player2 = player2;
    }

    public async Task MatchDialogue()
    {
        Console.WriteLine("test");
        string responseString = $"found{ID}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        await Task.Delay(5000);
        await Server.SendResponse(Player1.Client, responseString);
        await Server.SendResponse(Player2.Client, responseString);
        await Task.Delay(5000);
    }

    public async Task HandleJson(TcpClient client, string gridJson)
    {
        TcpClient enemyClient = GetEnemyPlayerClient(client);
        await Server.SendResponse(enemyClient, gridJson);
    }

    private TcpClient GetEnemyPlayerClient(TcpClient client)
    {
        if (client == Player1.Client) return Player2.Client;
        if (client == Player2.Client) return Player1.Client;
        return null;
    }
}