using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using QuizParty.Server.Hubs;

namespace QuizParty.Server.Game;

/// <summary>
/// Partie unique en mémoire : verrou, chrono et diffusion de l'état aux écrans connectés.
/// </summary>
public sealed class GameService(
    IHubContext<QuizHub> hub,
    IConfiguration config,
    IWebHostEnvironment env,
    LanAddress lan,
    TimeProvider clock,
    ILogger<GameService> logger)
{
    public const string HostGroup = "host";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // sans I ni O

    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _playerByConnection = [];
    private GameState? _game;
    private string? _contentTitle;
    private string? _contentError;
    private DateTimeOffset? _timerEndsAt;
    private int _timerDurationMs;
    private int _timerToken;

    /// <summary>Durée du chrono, identique pour toutes les questions (15 s par défaut).</summary>
    private int TimerSeconds => config.GetValue("Quiz:TimerSeconds", 15);

    private string ContentPath =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Quiz:QcmPath"] ?? "../../content/qcm.json"));

    // ---------- Hôte ----------

    public async Task CreateGame()
    {
        lock (_lock)
        {
            try
            {
                var content = QcmContent.Load(ContentPath);
                _game = new GameState(NewCode(), content.Questions);
                _contentTitle = content.Title;
                _contentError = null;
                logger.LogInformation("Nouvelle partie {Code} : {Count} questions", _game.Code, content.Questions.Count);
            }
            catch (QcmContentException ex)
            {
                _game = null;
                _contentError = ex.Message;
                logger.LogWarning("Questions invalides : {Error}", ex.Message);
            }
            _playerByConnection.Clear();
            CancelTimer();
        }
        await Broadcast();
    }

    public Task NextQuestion() => Mutate(g =>
    {
        CancelTimer();
        g.NextQuestion();
    });

    public Task StartTimer() => Mutate(g =>
    {
        if (g.Phase != Phase.Question || g.CurrentQuestion is null)
            throw new GameException("Pas de question ouverte.");

        var duration = TimeSpan.FromSeconds(TimerSeconds);
        var token = ++_timerToken;
        _timerDurationMs = (int)duration.TotalMilliseconds;
        _timerEndsAt = clock.GetUtcNow() + duration;
        _ = ExpireTimerAfter(duration, token);
    });

    public Task Reveal() => Mutate(g =>
    {
        CancelTimer();
        g.Reveal();
    });

    public Task SetScoresVisible(bool visible) => Mutate(g => g.SetScoresVisible(visible));

    public Task RemovePlayer(string playerId) => Mutate(g =>
    {
        g.RemovePlayer(playerId);
        foreach (var conn in _playerByConnection.Where(kv => kv.Value == playerId).Select(kv => kv.Key).ToList())
            _playerByConnection.Remove(conn);
    });

    // ---------- Joueurs ----------

    public async Task<JoinResult> Join(string connectionId, string code, string pseudo)
    {
        JoinResult result;
        lock (_lock)
        {
            if (_game is null) return new JoinResult(false, null, "Aucune partie en cours.");
            try
            {
                var player = _game.Join(code, pseudo, Guid.NewGuid().ToString("N"));
                _playerByConnection[connectionId] = player.Id;
                result = new JoinResult(true, player.Id, null);
            }
            catch (GameException ex)
            {
                return new JoinResult(false, null, ex.Message);
            }
        }
        await Broadcast();
        return result;
    }

    /// <summary>Reconnexion d'un téléphone avec l'identifiant gardé en local.</summary>
    public async Task<bool> Rejoin(string connectionId, string playerId)
    {
        lock (_lock)
        {
            var player = _game?.FindPlayer(playerId);
            if (player is null) return false;
            _playerByConnection[connectionId] = player.Id;
            player.Connected = true;
        }
        await Broadcast();
        return true;
    }

    public Task Answer(string connectionId, int choice) => Mutate(g =>
    {
        if (!_playerByConnection.TryGetValue(connectionId, out var playerId))
            throw new GameException("Tu n'as pas rejoint la partie.");
        g.Answer(playerId, choice);
    });

    public async Task Disconnected(string connectionId)
    {
        lock (_lock)
        {
            if (!_playerByConnection.Remove(connectionId, out var playerId)) return;
            if (!_playerByConnection.ContainsValue(playerId) && _game?.FindPlayer(playerId) is { } p)
                p.Connected = false;
        }
        await Broadcast();
    }

    // ---------- États envoyés aux écrans ----------

    public HostStateDto GetHostState()
    {
        lock (_lock) return BuildHostState();
    }

    public PlayerStateDto GetPlayerState(string connectionId)
    {
        lock (_lock)
        {
            _playerByConnection.TryGetValue(connectionId, out var playerId);
            return BuildPlayerState(playerId);
        }
    }

    private async Task Mutate(Action<GameState> action)
    {
        lock (_lock)
        {
            if (_game is null) throw new GameException("Aucune partie en cours.");
            action(_game);
        }
        await Broadcast();
    }

    private async Task Broadcast()
    {
        HostStateDto hostState;
        List<(string Connection, PlayerStateDto State)> players;
        lock (_lock)
        {
            hostState = BuildHostState();
            players = _playerByConnection.Select(kv => (kv.Key, BuildPlayerState(kv.Value))).ToList();
        }

        var sends = new List<Task> { hub.Clients.Group(HostGroup).SendAsync("hostState", hostState) };
        sends.AddRange(players.Select(p => hub.Clients.Client(p.Connection).SendAsync("playerState", p.State)));
        await Task.WhenAll(sends);
    }

    private HostStateDto BuildHostState()
    {
        var g = _game;
        if (g is null)
        {
            return new HostStateDto(false, null, null, null, _contentError, nameof(Phase.Lobby), [], null, null, null, 0,
                null, false, false, null);
        }

        var revealed = g.Phase == Phase.Reveal;
        return new HostStateDto(
            HasGame: true,
            Code: g.Code,
            JoinUrl: $"{lan.BaseUrl}/play?code={g.Code}",
            ContentTitle: _contentTitle,
            ContentError: null,
            Phase: g.Phase.ToString(),
            Players: g.Players.Select(p => new HostPlayerDto(p.Id, p.Pseudo, p.Connected, g.Answers.ContainsKey(p.Id))).ToList(),
            Question: QuestionOf(g),
            CorrectAnswer: revealed ? g.CurrentQuestion?.Answer : null,
            Distribution: revealed ? g.AnswerDistribution() : null,
            AnswersCount: g.Answers.Count,
            Timer: TimerOf(),
            HasNextQuestion: g.HasNextQuestion,
            ScoresVisible: g.ScoresVisible,
            // Le classement n'est envoyé que s'il est révélé : rien à espionner côté navigateur.
            Ranking: g.ScoresVisible ? g.Ranking() : null);
    }

    private PlayerStateDto BuildPlayerState(string? playerId)
    {
        var g = _game;
        var player = g?.FindPlayer(playerId);
        if (g is null || player is null)
            return new PlayerStateDto(false, g?.Code, null, null, nameof(Phase.Lobby), null, null, null, null, false, null);

        int? myAnswer = g.Answers.TryGetValue(player.Id, out var a) ? a : null;
        return new PlayerStateDto(
            Joined: true,
            Code: g.Code,
            PlayerId: player.Id,
            Pseudo: player.Pseudo,
            Phase: g.Phase.ToString(),
            Question: QuestionOf(g),
            MyAnswer: myAnswer,
            CorrectAnswer: g.Phase == Phase.Reveal ? g.CurrentQuestion?.Answer : null,
            Timer: TimerOf(),
            ScoresVisible: g.ScoresVisible,
            Ranking: g.ScoresVisible ? g.Ranking() : null);
    }

    private QuestionDto? QuestionOf(GameState g) =>
        g.CurrentQuestion is { } q && g.Phase != Phase.Finished
            ? new QuestionDto(g.QuestionIndex, g.Questions.Count, q.Text, q.Choices, TimerSeconds)
            : null;

    private TimerDto? TimerOf()
    {
        if (_timerEndsAt is not { } ends) return null;
        var remaining = (int)Math.Max(0, (ends - clock.GetUtcNow()).TotalMilliseconds);
        return new TimerDto(_timerDurationMs, remaining);
    }

    private void CancelTimer()
    {
        _timerToken++;
        _timerEndsAt = null;
    }

    private async Task ExpireTimerAfter(TimeSpan duration, int token)
    {
        await Task.Delay(duration, clock);
        lock (_lock)
        {
            if (token != _timerToken || _game is null) return;
            _timerEndsAt = null;
            _game.Lock();
        }
        await Broadcast();
    }

    private static string NewCode() =>
        string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
}
