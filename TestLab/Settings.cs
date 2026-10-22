using BepInEx.Configuration;
using TestLab.Core;

namespace TestLab;

/// <summary>
/// Einstellungen in BepInEx/config/de.spielenetzwerk.testlab.cfg. Die meisten lassen sich auch im Fenster umschalten
/// (BepInEx speichert sie dann sofort).
/// </summary>
internal static class Settings
{
    public static ConfigEntry<int> FillTo { get; private set; } = null!;
    public static ConfigEntry<bool> KeepBots { get; private set; } = null!;
    public static ConfigEntry<VoteMode> Votes { get; private set; } = null!;
    public static ConfigEntry<bool> QuickStart { get; private set; } = null!;
    public static ConfigEntry<bool> NoAutoEnd { get; private set; } = null!;
    public static ConfigEntry<bool> BlockCrewStats { get; private set; } = null!;
    public static ConfigEntry<bool> ShowWindow { get; private set; } = null!;
    public static ConfigEntry<bool> WindowLeft { get; private set; } = null!;

    public static void Bind(ConfigFile config)
    {
        FillTo = config.Bind("Bots", "AuffuellenBis", 8,
            new ConfigDescription("„Auffüllen“ füllt die Lobby bis zu so vielen Spielern (du mitgezählt), höchstens bis zur Lobby-Größe.",
                new AcceptableValueRange<int>(2, 15)));
        KeepBots = config.Bind("Bots", "Behalten", true,
            "Bots nach einer Runde automatisch wieder in die Lobby holen.");
        Votes = config.Bind("Bots", "Abstimmung", VoteMode.Skip,
            "Wie Bots in Meetings abstimmen: Skip, Zufall oder Keine. Wer gerade von dir gesteuert wird, stimmt nicht automatisch ab.");
        QuickStart = config.Bind("Lobby", "SofortStarten", true,
            "Mit Bots: kein Countdown, die Runde startet sofort.");
        NoAutoEnd = config.Bind("Spiel", "KeinAutomatischesEnde", false,
            "Mit Bots: die Runde endet nicht von selbst (Siegbedingungen aus) – beenden über das Fenster.");
        BlockCrewStats = config.Bind("CrewStats", "TestrundenNichtSenden", true,
            "Mit Bots: CrewStats zeichnet nichts auf und schickt nichts an Discord (keine XP, kein Auto-Mute, keine Statistik).");
        ShowWindow = config.Bind("Fenster", "Anzeigen", true,
            "TestLab-Fenster in lokalen Spielen zeigen (F1 blendet es ein und aus).");
        WindowLeft = config.Bind("Fenster", "Links", false,
            "Fenster links statt rechts zeigen (im Fenster mit ◀/▶ umschaltbar).");
    }
}
