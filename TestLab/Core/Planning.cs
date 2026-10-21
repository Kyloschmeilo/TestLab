using System;
using System.Collections.Generic;
using System.Linq;

namespace TestLab.Core;

// Spielunabhängige Logik – wird auch von Tests/ kompiliert (ohne Among Us und BepInEx).

/// <summary>Wie Bots in Meetings abstimmen (Config [Bots] Abstimmung).</summary>
public enum VoteMode
{
    /// <summary>Überspringen – wird der Skip abgelehnt (z.B. Hausregel „Kein Skip bei Notfall“), zufälliger Spieler.</summary>
    Skip,

    /// <summary>Zufälliger lebender Mitspieler.</summary>
    Zufall,

    /// <summary>Gar nicht abstimmen – das Meeting endet über die Zeit.</summary>
    Keine,
}

/// <param name="Vote">false = (noch) nicht abstimmen.</param>
/// <param name="Target">Spieler, für den gestimmt wird; null = Skip.</param>
public readonly record struct VoteChoice(bool Vote, byte? Target)
{
    public static readonly VoteChoice None = new(false, null);
    public static readonly VoteChoice SkipVote = new(true, null);
}

public static class VotePlanner
{
    /// <summary>Nach so vielen abgelehnten Stimmen gibt ein Bot auf (Meeting endet über die Zeit).</summary>
    public const int MaxAttempts = 3;

    /// <param name="attempt">0 = erste Stimme, 1+ = die vorige wurde zurückgenommen.</param>
    /// <param name="random">Zufallszahl in [0, max) – im Spiel RandomNumberGenerator.GetInt32.</param>
    public static VoteChoice Choose(VoteMode mode, byte voter, IReadOnlyList<byte> alive, int attempt, Func<int, int> random)
    {
        if (mode == VoteMode.Keine || attempt >= MaxAttempts) return VoteChoice.None;
        if (mode == VoteMode.Skip && attempt == 0) return VoteChoice.SkipVote;

        var others = alive.Where(id => id != voter).Distinct().ToList();
        if (others.Count == 0) return attempt == 0 ? VoteChoice.SkipVote : VoteChoice.None;
        return new VoteChoice(true, others[random(others.Count)]);
    }
}

public static class Roster
{
    /// <summary>Client-IDs der Bots beginnen hier – weit weg von echten Spielern (die zählen ab 1 hoch).</summary>
    public const int FirstBotClientId = 100;

    /// <summary>Kleinste freie Client-ID ab <see cref="FirstBotClientId"/>.</summary>
    public static int NextClientId(IEnumerable<int> used)
    {
        var taken = new HashSet<int>(used);
        var id = FirstBotClientId;
        while (taken.Contains(id)) id++;
        return id;
    }

    /// <summary>Kleinste freie Nummer für „Bot N“ (ab 1), damit Namen nach dem Entfernen wieder kurz werden.</summary>
    public static int NextNumber(IEnumerable<int> used)
    {
        var taken = new HashSet<int>(used);
        var number = 1;
        while (taken.Contains(number)) number++;
        return number;
    }

    /// <summary>Wie viele Bots „Auffüllen“ hinzufügt: bis <paramref name="target"/> Spieler, höchstens bis zur Lobby-Größe.</summary>
    public static int MissingForFill(int players, int target, int maxPlayers) =>
        Math.Max(0, Math.Min(target, maxPlayers) - players);

    /// <summary>Nächster Spieler nach <paramref name="current"/> (nach ID sortiert, am Ende wieder von vorne).</summary>
    public static byte? Next(IEnumerable<byte> players, byte current)
    {
        var sorted = players.Distinct().OrderBy(id => id).ToList();
        if (sorted.Count == 0) return null;
        foreach (var id in sorted)
            if (id > current)
                return id;
        return sorted[0] == current && sorted.Count == 1 ? null : sorted[0];
    }
}
