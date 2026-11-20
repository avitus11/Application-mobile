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

    /// <summary>Durée d'un tour de Synonymes (45 s par défaut).</summary>
    private int SynonymsSeconds => config.GetValue("Quiz:SynonymsSeconds", 45);

    private string ContentPath =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Quiz:QcmPath"] ?? "../../content/qcm.json"));

    private string SynonymsPath =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Quiz:SynonymsPath"] ?? "../../content/synonymes.json"));

    // ---------- Hôte ----------

    public async Task CreateGame()
    {
        lock (_lock)
        {
            try
            {
                var content = QcmContent.Load(ContentPath);
                var words = SynonymsContent.Load(SynonymsPath);
                _game = new GameState(NewCode(), content.Questions, words);
                _contentTitle = content.Title;
                _contentError = null;
                logger.LogInformation("Nouvelle partie {Code} : {Count} questions, {Words} mots",
                    _game.Code, content.Questions.Count, words.Count);
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
        StartTimerFor(TimerSeconds);
    });

    // ---------- Duos et Synonymes ----------

    public Task FormTeams() => Mutate(g => g.FormTeams());

    public Task StartSynonyms() => Mutate(g => g.StartSynonyms());

    public Task SelectTurn(int teamNumber) => Mutate(g => g.SelectTurn(teamNumber));

    public Task BackToTeams() => Mutate(g => g.BackToTeams());

    public Task StartTurn() => Mutate(g =>
    {
        g.StartTurn();
        StartTimerFor(SynonymsSeconds);
    });

    public Task WordFound(int wordSeq) => Mutate(g => AfterWord(g, () => g.WordFound(wordSeq)));

    public Task WordPassed(int wordSeq) => Mutate(g => AfterWord(g, () => g.WordPassed(wordSeq)));

    /// <summary>Trouvé / Passer depuis le téléphone : seul le joueur qui fait deviner peut le faire.</summary>
    public Task PlayerWord(string connectionId, int wordSeq, bool found) => Mutate(g =>
    {
        if (!_playerByConnection.TryGetValue(connectionId, out var playerId) || g.Turn?.GiverId != playerId)
            throw new GameException("Ce n'est pas toi qui fais deviner.");
        AfterWord(g, () =>
        {
            if (found) g.WordFound(wordSeq);
            else g.WordPassed(wordSeq);
        });
    });

    private void AfterWord(GameState g, Action action)
    {
        action();
        // Plus de mots : le tour s'est terminé tout seul, on coupe le chrono.
        if (g.Phase != Phase.SynonymsRunning) CancelTimer();
    }

    private void StartTimerFor(int seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        var token = ++_timerToken;
        _timerDurationMs = (int)duration.TotalMilliseconds;
        _timerEndsAt = clock.GetUtcNow() + duration;
        _ = ExpireTimerAfter(duration, token);
    }

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
                null, false, false, null, [], null, null, 0, false);
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
            Ranking: g.ScoresVisible ? g.Ranking() : null,
            Teams: g.Teams.Select(t => TeamOf(g, t)).ToList(),
            Turn: TurnOf(g),
            // Le mot part vers l'écran animateur pour qu'il puisse vérifier en cas de doute (il reste masqué à l'écran).
            Word: g.Phase == Phase.SynonymsRunning ? g.Turn?.Word : null,
            WordsLeft: g.WordsLeft,
            AllTurnsDone: g.AllTurnsDone);
    }

    private static TeamDto TeamOf(GameState g, Team t) => new(
        t.Number,
        t.MemberIds.Select(id => new TeamMemberDto(id, g.FindPlayer(id)?.Pseudo ?? "?")).ToList(),
        t.Found,
        t.TurnsDone,
        t.MemberIds.Count);

    private TurnDto? TurnOf(GameState g) => g.Turn is not { } turn
        ? null
        : new TurnDto(
            turn.Team.Number,
            turn.GiverId,
            g.FindPlayer(turn.GiverId)?.Pseudo ?? "?",
            turn.GuesserIds.Select(id => g.FindPlayer(id)?.Pseudo ?? "?").ToList(),
            turn.Found,
            turn.Passed,
            turn.WordSeq,
            SynonymsSeconds);

    private PlayerStateDto BuildPlayerState(string? playerId)
    {
        var g = _game;
        var player = g?.FindPlayer(playerId);
        if (g is null || player is null)
            return new PlayerStateDto(false, g?.Code, null, null, nameof(Phase.Lobby), null, null, null, null, false, null,
                null, null, null, null);

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
            Ranking: g.ScoresVisible ? g.Ranking() : null,
            MyTeam: g.TeamOf(player.Id) is { } team ? TeamOf(g, team) : null,
            Turn: TurnOf(g),
            Role: RoleOf(g, player.Id),
            Word: g.Phase == Phase.SynonymsRunning && g.Turn?.GiverId == player.Id ? g.Turn.Word : null);
    }

    private static string? RoleOf(GameState g, string playerId)
    {
        if (g.Turn is null || g.Phase is not (Phase.SynonymsReady or Phase.SynonymsRunning or Phase.SynonymsTurnEnd))
            return null;
        if (g.Turn.GiverId == playerId) return "giver";
        return g.Turn.Team.MemberIds.Contains(playerId) ? "guesser" : "spectator";
    }

    private QuestionDto? QuestionOf(GameState g) =>
        g.CurrentQuestion is { } q && g.Phase is Phase.Question or Phase.Locked or Phase.Reveal
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
            _game.OnTimerExpired();
        }
        await Broadcast();
    }

    private static string NewCode() =>
        string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
}
