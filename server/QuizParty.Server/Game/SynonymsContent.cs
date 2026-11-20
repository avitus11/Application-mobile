using System.Text.Json;

namespace QuizParty.Server.Game;

public sealed record SynonymsFile(List<string> Words);

/// <summary>Lecture de content/synonymes.json (la liste des mots à faire deviner).</summary>
public static class SynonymsContent
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static List<string> Load(string path)
    {
        if (!File.Exists(path))
            throw new QcmContentException($"Fichier de mots introuvable : {Path.GetFullPath(path)}");
        return Parse(File.ReadAllText(path));
    }

    public static List<string> Parse(string json)
    {
        SynonymsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SynonymsFile>(json, Options);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (ligne {line + 1})" : "";
            throw new QcmContentException($"synonymes.json : JSON invalide{where} : {ex.Message}");
        }

        var words = (file?.Words ?? [])
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (words.Count == 0)
            throw new QcmContentException("synonymes.json ne contient aucun mot (clé \"words\").");
        return words;
    }
}
