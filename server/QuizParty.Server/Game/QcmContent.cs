using System.Text.Json;

namespace QuizParty.Server.Game;

/// <summary>Une question du QCM telle qu'écrite dans content/qcm.json.</summary>
public sealed record QcmQuestion(string Text, string[] Choices, int Answer, int Duration);

public sealed record QcmFile(string? Title, List<QcmQuestion> Questions);

public sealed class QcmContentException(string message) : Exception(message);

public static class QcmContent
{
    public const int DefaultDuration = 20;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static QcmFile Load(string path)
    {
        if (!File.Exists(path))
            throw new QcmContentException($"Fichier de questions introuvable : {Path.GetFullPath(path)}");

        return Parse(File.ReadAllText(path));
    }

    /// <summary>Lit et valide le JSON. Lève une QcmContentException avec un message lisible en cas d'erreur.</summary>
    public static QcmFile Parse(string json)
    {
        QcmFile? file;
        try
        {
            file = JsonSerializer.Deserialize<QcmFile>(json, Options);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (ligne {line + 1})" : "";
            throw new QcmContentException($"JSON invalide{where} : {ex.Message}");
        }

        if (file?.Questions is null || file.Questions.Count == 0)
            throw new QcmContentException("Le fichier ne contient aucune question (clé \"questions\").");

        var errors = new List<string>();
        var normalized = new List<QcmQuestion>();
        for (var i = 0; i < file.Questions.Count; i++)
        {
            var q = file.Questions[i];
            var n = i + 1;
            if (q is null) { errors.Add($"Question {n} : vide."); continue; }
            if (string.IsNullOrWhiteSpace(q.Text)) errors.Add($"Question {n} : texte manquant.");
            if (q.Choices is null || q.Choices.Length != 4)
                errors.Add($"Question {n} : il faut exactement 4 choix.");
            else if (q.Choices.Any(string.IsNullOrWhiteSpace))
                errors.Add($"Question {n} : un des choix est vide.");
            if (q.Answer is < 0 or > 3)
                errors.Add($"Question {n} : \"answer\" doit valoir 0, 1, 2 ou 3 (A, B, C ou D).");
            if (q.Duration < 0)
                errors.Add($"Question {n} : la durée ne peut pas être négative.");

            normalized.Add(q with { Duration = q.Duration == 0 ? DefaultDuration : q.Duration });
        }

        if (errors.Count > 0)
            throw new QcmContentException(string.Join(Environment.NewLine, errors));

        return file with { Questions = normalized };
    }
}
