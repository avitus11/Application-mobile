using QuizParty.Server.Game;

namespace QuizParty.Tests;

public class QcmContentTests
{
    [Fact]
    public void Parses_comments_and_trailing_commas()
    {
        const string json = """
            {
              // commentaire autorisé
              "title": "Test",
              "questions": [
                { "text": "Q", "choices": ["a", "b", "c", "d"], "answer": 3 },
              ]
            }
            """;

        var file = QcmContent.Parse(json);

        Assert.Equal("Test", file.Title);
        Assert.Equal(3, file.Questions[0].Answer);
    }

    [Fact]
    public void Reports_every_problem_with_question_numbers()
    {
        const string json = """
            { "questions": [
              { "text": "", "choices": ["a", "b", "c"], "answer": 7 }
            ] }
            """;

        var ex = Assert.Throws<QcmContentException>(() => QcmContent.Parse(json));

        Assert.Contains("Question 1 : texte manquant.", ex.Message);
        Assert.Contains("exactement 4 choix", ex.Message);
        Assert.Contains("\"answer\" doit valoir", ex.Message);
    }

    [Fact]
    public void Invalid_json_gives_a_readable_error()
    {
        var ex = Assert.Throws<QcmContentException>(() => QcmContent.Parse("{ \"questions\": [ "));
        Assert.StartsWith("JSON invalide", ex.Message);
    }

    [Fact]
    public void Sample_content_file_is_valid()
    {
        // Remonte depuis le dossier de sortie des tests jusqu'à la racine du dépôt.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "content", "qcm.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var file = QcmContent.Load(Path.Combine(dir!.FullName, "content", "qcm.json"));
        Assert.Equal(10, file.Questions.Count);
    }
}
