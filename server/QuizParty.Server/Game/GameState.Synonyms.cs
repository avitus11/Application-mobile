namespace QuizParty.Server.Game;

/// <summary>Un duo (ou le trio quand le nombre de joueurs est impair).</summary>
public sealed class Team(int number, List<string> memberIds)
{
    public int Number { get; } = number;
    public List<string> MemberIds { get; } = memberIds;
    /// <summary>Mots trouvés par l'équipe sur toute l'épreuve.</summary>
    public int Found { get; set; }
    /// <summary>Joueurs qui ont déjà fait deviner : chacun fait deviner une fois.</summary>
    public HashSet<string> GiversDone { get; } = [];

    public string? NextGiver => MemberIds.FirstOrDefault(id => !GiversDone.Contains(id));
    public int TurnsDone => MemberIds.Count(GiversDone.Contains);
}

/// <summary>Le tour en cours : un joueur fait deviner un mot à son ou ses partenaires.</summary>
public sealed class SynonymsTurn(Team team, string giverId)
{
    public Team Team { get; } = team;
    public string GiverId { get; } = giverId;
    public IEnumerable<string> GuesserIds => Team.MemberIds.Where(id => id != GiverId);
    public string? Word { get; set; }
    /// <summary>Numéro du mot affiché : évite qu'un double appui (animateur + téléphone) saute un mot.</summary>
    public int WordSeq { get; set; }
    public int Found { get; set; }
    public int Passed { get; set; }
}

public sealed partial class GameState
{
    public const int PointsPerSynonym = 1;

    private readonly Random _random;
    private readonly Queue<string> _words;
    private readonly List<Team> _teams = [];

    public IReadOnlyList<Team> Teams => _teams;
    public SynonymsTurn? Turn { get; private set; }
    public int WordsLeft => _words.Count;
    public bool AllTurnsDone => _teams.Count > 0 && _teams.All(t => t.NextGiver is null);

    public Team? TeamOf(string? playerId) =>
        playerId is null ? null : _teams.FirstOrDefault(t => t.MemberIds.Contains(playerId));

    /// <summary>
    /// Duos à partir du classement du QCM : 1er + dernier, 2e + avant-dernier…
    /// Nombre impair : le joueur du milieu rejoint le duo du 1er (trio).
    /// Les ex æquo sont départagés au hasard.
    /// </summary>
    public void FormTeams()
    {
        if (Phase != Phase.Finished) throw new GameException("Termine d'abord le QCM.");
        if (_players.Count < 2) throw new GameException("Il faut au moins 2 joueurs pour former des duos.");

        var ordered = _players
            .Select(p => (Player: p, Tie: _random.Next()))
            .OrderByDescending(x => x.Player.Score)
            .ThenBy(x => x.Tie)
            .Select(x => x.Player.Id)
            .ToList();

        _teams.Clear();
        var n = ordered.Count;
        for (var i = 0; i < n / 2; i++)
            _teams.Add(new Team(i + 1, [ordered[i], ordered[n - 1 - i]]));
        if (n % 2 == 1)
            _teams[0].MemberIds.Insert(1, ordered[n / 2]);

        Phase = Phase.Duos;
    }

    public void StartSynonyms()
    {
        if (Phase != Phase.Duos) throw new GameException("Forme d'abord les duos.");
        Phase = Phase.SynonymsIdle;
    }

    /// <summary>L'animateur choisit l'équipe qui joue ; le prochain membre qui n'a pas encore fait deviner s'y colle.</summary>
    public void SelectTurn(int teamNumber)
    {
        if (Phase is not (Phase.SynonymsIdle or Phase.SynonymsTurnEnd or Phase.SynonymsReady))
            throw new GameException("Impossible de choisir une équipe maintenant.");
        var team = _teams.FirstOrDefault(t => t.Number == teamNumber) ?? throw new GameException("Équipe inconnue.");
        if (team.MemberIds.Count < 2) throw new GameException("Cette équipe n'a plus assez de joueurs.");
        var giver = team.NextGiver ?? throw new GameException("Cette équipe a déjà joué tous ses tours.");

        Turn = new SynonymsTurn(team, giver);
        Phase = Phase.SynonymsReady;
    }

    /// <summary>Retour à la liste des équipes (fin de tour, ou tour choisi par erreur).</summary>
    public void BackToTeams()
    {
        if (Phase is not (Phase.SynonymsReady or Phase.SynonymsTurnEnd))
            throw new GameException("Un tour est en cours.");
        Turn = null;
        Phase = Phase.SynonymsIdle;
    }

    /// <summary>Lance le tour : premier mot affiché chez celui qui fait deviner.</summary>
    public void StartTurn()
    {
        if (Phase != Phase.SynonymsReady || Turn is null) throw new GameException("Aucun tour prêt.");
        if (_words.Count == 0) throw new GameException("Il n'y a plus de mots dans content/synonymes.json.");
        Phase = Phase.SynonymsRunning;
        DrawWord();
    }

    /// <summary>Mot trouvé : +1 point pour chaque membre de l'équipe, mot suivant.</summary>
    public void WordFound(int wordSeq)
    {
        if (!IsCurrentWord(wordSeq)) return;
        Turn!.Found++;
        Turn.Team.Found++;
        foreach (var id in Turn.Team.MemberIds)
            if (FindPlayer(id) is { } p) p.Score += PointsPerSynonym;
        DrawWord();
    }

    /// <summary>Mot passé : pas de pénalité, mot suivant.</summary>
    public void WordPassed(int wordSeq)
    {
        if (!IsCurrentWord(wordSeq)) return;
        Turn!.Passed++;
        DrawWord();
    }

    /// <summary>Fin du chrono (ou plus de mots) : le tour est terminé.</summary>
    public void EndTurn()
    {
        if (Phase != Phase.SynonymsRunning || Turn is null) return;
        Turn.Team.GiversDone.Add(Turn.GiverId);
        Turn.Word = null;
        Phase = Phase.SynonymsTurnEnd;
    }

    /// <summary>Appelé quand le chrono arrive à zéro, quelle que soit l'épreuve.</summary>
    public void OnTimerExpired()
    {
        if (Phase == Phase.Question) Lock();
        else if (Phase == Phase.SynonymsRunning) EndTurn();
    }

    private bool IsCurrentWord(int wordSeq) =>
        Phase == Phase.SynonymsRunning && Turn is not null && Turn.WordSeq == wordSeq;

    private void DrawWord()
    {
        if (_words.Count == 0)
        {
            EndTurn();
            return;
        }
        Turn!.Word = _words.Dequeue();
        Turn.WordSeq++;
    }
}
