namespace QuizParty.Server.Game;

public sealed record TimerDto(int DurationMs, int RemainingMs);

public sealed record QuestionDto(int Index, int Count, string Text, string[] Choices, int Duration);

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
    IReadOnlyList<RankingEntry>? Ranking);

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
    IReadOnlyList<RankingEntry>? Ranking);

public sealed record JoinResult(bool Ok, string? PlayerId, string? Error);
