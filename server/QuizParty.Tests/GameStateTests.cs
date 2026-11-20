using QuizParty.Server.Game;

namespace QuizParty.Tests;

public class GameStateTests
{
    private static readonly QcmQuestion[] Questions =
    [
        new("Q1", ["a", "b", "c", "d"], Answer: 2),
        new("Q2", ["a", "b", "c", "d"], Answer: 0),
    ];

    private static GameState NewGame() => new("ABCD", Questions);

    [Fact]
    public void Join_with_wrong_code_is_refused()
    {
        var game = NewGame();
        var ex = Assert.Throws<GameException>(() => game.Join("ZZZZ", "Alice", "1"));
        Assert.Equal("Code de salon incorrect.", ex.Message);
    }

    [Fact]
    public void Join_accepts_code_in_lower_case_and_trims_pseudo()
    {
        var game = NewGame();
        var player = game.Join(" abcd ", "  Alice ", "1");
        Assert.Equal("Alice", player.Pseudo);
    }

    [Fact]
    public void Pseudo_must_be_unique_ignoring_case()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "1");
        Assert.Throws<GameException>(() => game.Join("ABCD", "ALICE", "2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnPseudoBeaucoupTropLong")]
    public void Invalid_pseudo_is_refused(string pseudo)
    {
        Assert.Throws<GameException>(() => NewGame().Join("ABCD", pseudo, "1"));
    }

    [Fact]
    public void Correct_answer_gives_one_point_and_wrong_answer_none()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.Join("ABCD", "Bob", "b");
        game.Join("ABCD", "Chloé", "c"); // ne répond pas

        game.NextQuestion();
        game.Answer("a", 2);
        game.Answer("b", 1);
        game.Reveal();

        Assert.Equal(1, game.FindPlayer("a")!.Score);
        Assert.Equal(0, game.FindPlayer("b")!.Score);
        Assert.Equal(0, game.FindPlayer("c")!.Score);
    }

    [Fact]
    public void Player_can_change_answer_until_locked()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.NextQuestion();
        game.Answer("a", 0);
        game.Answer("a", 2);
        game.Lock();

        Assert.Throws<GameException>(() => game.Answer("a", 1));
        game.Reveal();
        Assert.Equal(1, game.FindPlayer("a")!.Score);
    }

    [Fact]
    public void Cannot_skip_a_question_before_reveal()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.NextQuestion();
        Assert.Throws<GameException>(game.NextQuestion);
    }

    [Fact]
    public void Answers_are_cleared_between_questions_and_game_finishes_after_last()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.NextQuestion();
        game.Answer("a", 2);
        game.Reveal();

        game.NextQuestion();
        Assert.Empty(game.Answers);
        Assert.Equal(1, game.QuestionIndex);
        game.Reveal();

        Assert.False(game.HasNextQuestion);
        game.NextQuestion();
        Assert.Equal(Phase.Finished, game.Phase);
    }

    [Fact]
    public void Ranking_shares_rank_between_tied_players()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.Join("ABCD", "Bob", "b");
        game.Join("ABCD", "Chloé", "c");

        game.NextQuestion();
        game.Answer("a", 2);
        game.Answer("b", 2);
        game.Reveal();

        var ranking = game.Ranking();
        Assert.Equal([1, 1, 3], ranking.Select(r => r.Rank));
        Assert.Equal("Chloé", ranking[2].Pseudo);
    }

    [Fact]
    public void Distribution_counts_answers_per_choice()
    {
        var game = NewGame();
        game.Join("ABCD", "Alice", "a");
        game.Join("ABCD", "Bob", "b");
        game.NextQuestion();
        game.Answer("a", 3);
        game.Answer("b", 3);

        Assert.Equal([0, 0, 0, 2], game.AnswerDistribution());
    }
}
