using System.Text;

namespace BlazorArcade.Shared.Scores;

public sealed record ScoreEntry(int Rank, string Player, int Score, DateTimeOffset AchievedAt);

public sealed record SubmitScoreRequest(string Player, int Score);

/// <summary>Where the submitted score landed (0 when it did not make the stored board), plus the current top list.</summary>
public sealed record SubmitScoreResponse(int Rank, IReadOnlyList<ScoreEntry> Top);

public static class ScoreGames
{
    public const string CryptCrawler = "cryptcrawler";
    public const string Knightmare = "knightmare";

    /// <summary>
    /// Games that may post scores, with the highest score the server will believe.
    /// The cap is a cheap plausibility check, not anti-cheat.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> MaxScore = new Dictionary<string, int>
    {
        [CryptCrawler] = 100_000,
        [Knightmare] = 9_999_999,
        ["tetris"] = 9_999_999,
        ["breakout"] = 999_999,
        ["pacman"] = 999_999,
        ["2048"] = 9_999_999,
        ["donkeykong"] = 999_999,
        ["flappybird"] = 9_999,
        ["blockblast"] = 9_999_999,
        ["bubbleshooter"] = 9_999_999,
    };

    public const int MaxNameLength = 16;

    /// <summary>Trims and tidies a nickname; returns null when nothing usable is left.</summary>
    public static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var sb = new StringBuilder();
        bool space = false;
        foreach (char c in name.Trim())
        {
            if (char.IsControl(c)) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!space) sb.Append(' ');
                space = true;
                continue;
            }
            space = false;
            sb.Append(c);
        }
        string result = sb.ToString().Trim();
        if (result.Length > MaxNameLength) result = result[..MaxNameLength].TrimEnd();
        return result.Length == 0 ? null : result;
    }
}
