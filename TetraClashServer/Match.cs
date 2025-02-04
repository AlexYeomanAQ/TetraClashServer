using System.Net.Sockets;
using TetraClashServer;

public class Match
{
    private Player Player1 { get; }
    private Player Player2 { get; }

    public Match(Player player1, Player player2)
    {
        Player1 = player1;
        Player2 = player2;
    }

    public async Task MatchDialogue()
    {
        Console.WriteLine($"Starting match: {Player1.Name} vs {Player2.Name}");
        await Server.SendResponse(Player1.Client, "Match started! You are playing against " + Player2.Name);
        await Server.SendResponse(Player2.Client, "Match started! You are playing against " + Player1.Name);

        await Task.Delay(1000);
        Console.WriteLine($"Match ended: {Player1.Name} vs {Player2.Name}");
        await Server.SendResponse(Player1.Client, "Match ended!");
        await Server.SendResponse(Player2.Client, "Match ended!");
    }

    public async Task HandleJson(TcpClient client, string gridJson)
    {
        TcpClient enemyClient = GetEnemyPlayerClient(client);
        await Server.SendResponse(client, gridJson);
    }

    private TcpClient GetEnemyPlayerClient(TcpClient client)
    {
        if (client == Player1.Client) return Player2.Client;
        if (client == Player2.Client) return Player1.Client;
        return null;
    }
}