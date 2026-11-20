using System.Net;
using Microsoft.AspNetCore.SignalR;
using QuizParty.Server.Game;

namespace QuizParty.Server.Hubs;

/// <summary>
/// Point d'entrée temps réel. Les commandes animateur ne sont acceptées que depuis le PC qui fait tourner le serveur.
/// </summary>
public sealed class QuizHub(GameService game, IConfiguration config) : Hub
{
    // ---------- Animateur ----------

    public async Task<HostStateDto> HostConnect()
    {
        EnsureHost();
        await Groups.AddToGroupAsync(Context.ConnectionId, GameService.HostGroup);
        return game.GetHostState();
    }

    public Task CreateGame() => Host(game.CreateGame);
    public Task NextQuestion() => Host(game.NextQuestion);
    public Task StartTimer() => Host(game.StartTimer);
    public Task RevealAnswer() => Host(game.Reveal);
    public Task SetScoresVisible(bool visible) => Host(() => game.SetScoresVisible(visible));
    public Task RemovePlayer(string playerId) => Host(() => game.RemovePlayer(playerId));

    // ---------- Joueurs ----------

    public Task<JoinResult> Join(string code, string pseudo) => game.Join(Context.ConnectionId, code, pseudo);

    public Task<bool> Rejoin(string playerId) => game.Rejoin(Context.ConnectionId, playerId);

    public PlayerStateDto GetPlayerState() => game.GetPlayerState(Context.ConnectionId);

    public Task Answer(int choice) => Wrap(() => game.Answer(Context.ConnectionId, choice));

    public override Task OnDisconnectedAsync(Exception? exception) => game.Disconnected(Context.ConnectionId);

    // ---------- Outils ----------

    private Task Host(Func<Task> action)
    {
        EnsureHost();
        return Wrap(action);
    }

    private static async Task Wrap(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (GameException ex)
        {
            // HubException : le message est transmis tel quel au navigateur.
            throw new HubException(ex.Message);
        }
    }

    private void EnsureHost()
    {
        if (config.GetValue("Quiz:AllowRemoteHost", false)) return;
        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip))
            throw new HubException("L'écran animateur ne s'ouvre que sur le PC qui fait tourner le serveur.");
    }
}
