using QuizParty.Server.Game;

namespace QuizParty.Tests;

public class SynonymsTests
{
    private static readonly QcmQuestion[] Questions = [new("Q", ["a", "b", "c", "d"], Answer: 0)];

    /// <summary>Partie avec QCM terminé : les joueurs listés de 0 à n-1 ont des scores décroissants.</summary>
    private static GameState GameAfterQcm(int playerCount, string[]? words = null)
    {
        var game = new GameState("ABCD", Questions, words ?? ["chat", "chien", "maison", "arbre"], new Random(42));
        for (var i = 0; i < playerCount; i++)
        {
            var p = game.Join("ABCD", $"J{i}", $"p{i}");
            p.Score = 100 - i;
        }
        game.NextQuestion();
        game.Reveal();
        game.NextQuestion(); // plus de question : Finished
        return game;
    }

    private static string[] Members(Team t) => [.. t.MemberIds];

    [Fact]
    public void Teams_pair_first_with_last()
    {
        var game = GameAfterQcm(6);
        game.FormTeams();

        Assert.Equal(Phase.Duos, game.Phase);
        Assert.Equal(3, game.Teams.Count);
        Assert.Equal(["p0", "p5"], Members(game.Teams[0]));
        Assert.Equal(["p1", "p4"], Members(game.Teams[1]));
        Assert.Equal(["p2", "p3"], Members(game.Teams[2]));
    }

    [Fact]
    public void Odd_player_count_makes_one_trio_with_the_first()
    {
        var game = GameAfterQcm(5);
        game.FormTeams();

        Assert.Equal(2, game.Teams.Count);
        Assert.Equal(["p0", "p2", "p4"], Members(game.Teams[0]));
        Assert.Equal(["p1", "p3"], Members(game.Teams[1]));
    }

    [Fact]
    public void Teams_cannot_be_formed_before_the_end_of_the_qcm()
    {
        var game = new GameState("ABCD", Questions, ["x"]);
        game.Join("ABCD", "A", "a");
        game.Join("ABCD", "B", "b");
        Assert.Throws<GameException>(game.FormTeams);
    }

    [Fact]
    public void Each_member_gives_once_then_the_team_is_done()
    {
        var game = GameAfterQcm(2, ["un", "deux", "trois"]);
        game.FormTeams();
        game.StartSynonyms();

        game.SelectTurn(1);
        Assert.Equal("p0", game.Turn!.GiverId);
        game.StartTurn();
        game.EndTurn();
        game.BackToTeams();

        game.SelectTurn(1);
        Assert.Equal("p1", game.Turn!.GiverId);
        game.StartTurn();
        game.EndTurn();

        Assert.True(game.AllTurnsDone);
        Assert.Throws<GameException>(() => game.SelectTurn(1));
    }

    [Fact]
    public void Found_word_gives_a_point_to_every_member_and_pass_costs_nothing()
    {
        var game = GameAfterQcm(3); // trio p0, p1, p2
        game.FormTeams();
        game.StartSynonyms();
        game.SelectTurn(1);
        game.StartTurn();

        var before = game.Players.ToDictionary(p => p.Id, p => p.Score);
        var first = game.Turn!.Word;
        game.WordFound(game.Turn.WordSeq);
        Assert.NotEqual(first, game.Turn.Word);
        game.WordPassed(game.Turn.WordSeq);

        Assert.All(game.Players, p => Assert.Equal(before[p.Id] + 1, p.Score));
        Assert.Equal(1, game.Turn.Found);
        Assert.Equal(1, game.Turn.Passed);
        Assert.Equal(1, game.Teams[0].Found);
    }

    [Fact]
    public void Double_press_on_the_same_word_counts_once()
    {
        var game = GameAfterQcm(2);
        game.FormTeams();
        game.StartSynonyms();
        game.SelectTurn(1);
        game.StartTurn();

        var seq = game.Turn!.WordSeq;
        game.WordFound(seq);
        game.WordFound(seq); // deuxième appui (animateur + téléphone) ignoré

        Assert.Equal(1, game.Turn.Found);
    }

    [Fact]
    public void Each_word_is_used_once_and_turn_ends_when_words_run_out()
    {
        var game = GameAfterQcm(2, ["un", "deux"]);
        game.FormTeams();
        game.StartSynonyms();
        game.SelectTurn(1);
        game.StartTurn();

        var seen = new List<string> { game.Turn!.Word! };
        game.WordPassed(game.Turn.WordSeq);
        seen.Add(game.Turn.Word!);
        game.WordPassed(game.Turn.WordSeq);

        Assert.Equal(2, seen.Distinct().Count());
        Assert.Equal(Phase.SynonymsTurnEnd, game.Phase);
        Assert.Null(game.Turn.Word);
    }

    [Fact]
    public void Timer_expiry_ends_the_turn()
    {
        var game = GameAfterQcm(2);
        game.FormTeams();
        game.StartSynonyms();
        game.SelectTurn(1);
        game.StartTurn();

        game.OnTimerExpired();

        Assert.Equal(Phase.SynonymsTurnEnd, game.Phase);
        Assert.Contains("p0", game.Teams[0].GiversDone);
    }
}
