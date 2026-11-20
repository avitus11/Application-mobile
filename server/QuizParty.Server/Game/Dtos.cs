namespace QuizParty.Server.Game;

public sealed record TimerDto(int DurationMs, int RemainingMs);

public sealed record QuestionDto(int Index, int Count, string Text, string[] Choices, int Duration);

public sealed record TeamMemberDto(string Id, string Pseudo);

public sealed record TeamDto(int Number, IReadOnlyList<TeamMemberDto> Members, int Found, int TurnsDone, int TurnsTotal);

public sealed record TurnDto(int TeamNumber, string GiverId, string GiverPseudo, IReadOnlyList<string> Guessers, int Found, int Passed, int WordSeq, int Seconds);

public sealed record HostPlayerDto(string Id, string Pseudo, bool Connected, bool HasAnswered);

public sealed record HostStateDto(
    bool HasGame,
    string? Code,
    string? JoinUrl,
    string? ContentTitle,
    string? ContentError,
    string Phase,
    IReadOnlyList<HostPlayerDto> Players,
    QuestionDto? Question,
    int? CorrectAnswer,
    int[]? Distribution,
    int AnswersCount,
    TimerDto? Timer,
    bool HasNextQuestion,
    bool ScoresVisible,
    IReadOnlyList<RankingEntry>? Ranking,
    IReadOnlyList<TeamDto> Teams,
    TurnDto? Turn,
    string? Word,
    int WordsLeft,
    bool AllTurnsDone);

public sealed record PlayerStateDto(
    bool Joined,
    string? Code,
    string? PlayerId,
    string? Pseudo,
    string Phase,
    QuestionDto? Question,
    int? MyAnswer,
    int? CorrectAnswer,
    TimerDto? Timer,
    bool ScoresVisible,
    IReadOnlyList<RankingEntry>? Ranking,
    TeamDto? MyTeam,
    TurnDto? Turn,
    /// <summary>"giver", "guesser" ou "spectator" pendant un tour de Synonymes.</summary>
    string? Role,
    /// <summary>Le mot à faire deviner : envoyé uniquement au joueur qui fait deviner.</summary>
    string? Word);

public sealed record JoinResult(bool Ok, string? PlayerId, string? Error);
