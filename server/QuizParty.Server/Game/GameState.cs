namespace QuizParty.Server.Game;

public enum Phase
{
    /// <summary>Salon ouvert, les joueurs arrivent.</summary>
    Lobby,
    /// <summary>Une question est affichée, les joueurs peuvent répondre.</summary>
    Question,
    /// <summary>Réponses bloquées (fin du chrono ou bouton), bonne réponse pas encore montrée.</summary>
    Locked,
    /// <summary>Bonne réponse affichée, points attribués.</summary>
    Reveal,
    /// <summary>Plus de questions : fin du QCM.</summary>
    Finished,
}

public sealed class Player(string id, string pseudo)
{
    public string Id { get; } = id;
    public string Pseudo { get; } = pseudo;
    public int Score { get; set; }
    public bool Connected { get; set; } = true;
}

public sealed record RankingEntry(int Rank, string PlayerId, string Pseudo, int Score);

public sealed class GameException(string message) : Exception(message);

/// <summary>
/// État d'une partie, sans réseau ni horloge : toute la logique de jeu testable est ici.
/// </summary>
public sealed class GameState
{
    public const int MaxPseudoLength = 16;
    public const int PointsPerCorrectAnswer = 1;

    private readonly List<Player> _players = [];
    private readonly Dictionary<string, int> _answers = [];

    public GameState(string code, IReadOnlyList<QcmQuestion> questions)
    {
        Code = code;
        Questions = questions;
    }

    public string Code { get; }
    public IReadOnlyList<QcmQuestion> Questions { get; }
    public Phase Phase { get; private set; } = Phase.Lobby;
    public int QuestionIndex { get; private set; } = -1;
    public bool ScoresVisible { get; private set; }
    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyDictionary<string, int> Answers => _answers;

    public QcmQuestion? CurrentQuestion =>
        QuestionIndex >= 0 && QuestionIndex < Questions.Count ? Questions[QuestionIndex] : null;

    public bool HasNextQuestion => QuestionIndex + 1 < Questions.Count;

    public Player? FindPlayer(string? id) => id is null ? null : _players.FirstOrDefault(p => p.Id == id);

    public Player Join(string code, string pseudo, string newId)
    {
        if (!string.Equals(code?.Trim(), Code, StringComparison.OrdinalIgnoreCase))
            throw new GameException("Code de salon incorrect.");

        var clean = (pseudo ?? "").Trim();
        if (clean.Length == 0) throw new GameException("Choisis un pseudo.");
        if (clean.Length > MaxPseudoLength) throw new GameException($"Pseudo trop long ({MaxPseudoLength} caractères max).");
        if (_players.Any(p => string.Equals(p.Pseudo, clean, StringComparison.OrdinalIgnoreCase)))
            throw new GameException("Ce pseudo est déjà pris.");

        var player = new Player(newId, clean);
        _players.Add(player);
        return player;
    }

    public void RemovePlayer(string id)
    {
        _players.RemoveAll(p => p.Id == id);
        _answers.Remove(id);
    }

    /// <summary>Affiche la question suivante et ouvre les réponses.</summary>
    public void NextQuestion()
    {
        if (Phase is Phase.Question or Phase.Locked)
            throw new GameException("Révèle d'abord la réponse de la question en cours.");
        if (!HasNextQuestion)
        {
            Phase = Phase.Finished;
            return;
        }

        QuestionIndex++;
        _answers.Clear();
        Phase = Phase.Question;
    }

    public void Answer(string playerId, int choice)
    {
        if (Phase != Phase.Question) throw new GameException("Les réponses sont fermées.");
        if (FindPlayer(playerId) is null) throw new GameException("Joueur inconnu.");
        if (choice is < 0 or > 3) throw new GameException("Choix invalide.");
        // Un joueur peut changer d'avis tant que les réponses sont ouvertes.
        _answers[playerId] = choice;
    }

    public void Lock()
    {
        if (Phase == Phase.Question) Phase = Phase.Locked;
    }

    /// <summary>Montre la bonne réponse et attribue les points (1 par bonne réponse, sans bonus de vitesse).</summary>
    public void Reveal()
    {
        if (Phase is not (Phase.Question or Phase.Locked))
            throw new GameException("Aucune question en cours.");

        var correct = CurrentQuestion!.Answer;
        foreach (var (playerId, choice) in _answers)
        {
            if (choice == correct && FindPlayer(playerId) is { } p)
                p.Score += PointsPerCorrectAnswer;
        }
        Phase = Phase.Reveal;
    }

    public void SetScoresVisible(bool visible) => ScoresVisible = visible;

    public int[] AnswerDistribution()
    {
        var counts = new int[4];
        foreach (var choice in _answers.Values) counts[choice]++;
        return counts;
    }

    /// <summary>Classement par score décroissant. Les ex aequo partagent le même rang (1, 1, 3…).</summary>
    public IReadOnlyList<RankingEntry> Ranking()
    {
        var ordered = _players
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Pseudo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<RankingEntry>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i > 0 && ordered[i].Score == ordered[i - 1].Score ? result[i - 1].Rank : i + 1;
            result.Add(new RankingEntry(rank, ordered[i].Id, ordered[i].Pseudo, ordered[i].Score));
        }
        return result;
    }
}
