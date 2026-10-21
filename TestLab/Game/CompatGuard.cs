using System;
using System.Linq;
using HarmonyLib;

namespace TestLab.Game;

/// <summary>
/// CrewStats würde Testrunden mit Bots aufzeichnen und an den Discord-Bot schicken (XP, Statistik, Auto-Mute).
/// Ist CrewStats installiert, werden seine Einstiegspunkte hier per Name gepatcht und in Testspielen übersprungen.
/// Benennt CrewStats die Methoden um, zeigt das Fenster eine Warnung (dann bitte hier anpassen).
/// </summary>
internal static class CompatGuard
{
    public const string CrewStatsId = "de.amongusmanager.crewstats";

    private static readonly (string Type, string Method)[] CrewStatsMethods =
    {
        ("CrewStats.GameRecorder", "OnGameStart"), // Aufzeichnung starten
        ("CrewStats.SettingsReporter", "OnGameStart"), // Lobby-Einstellungen senden
        ("CrewStats.AutoMute", "Send"), // Discord muten
        ("CrewStats.RoundUploader", "SaveAndSend"), // Runde senden (zur Sicherheit, falls doch aufgezeichnet)
    };

    private static bool _loggedBlock;

    public static bool CrewStatsLoaded { get; private set; }

    /// <summary>Leer = alles in Ordnung, sonst Warnung fürs Fenster.</summary>
    public static string Warning { get; private set; } = "";

    public static bool Blocking => CrewStatsLoaded && Settings.BlockCrewStats.Value && Session.IsTestGame;

    public static void Apply(Harmony harmony)
    {
        var assembly = FindAssembly("CrewStats");
        if (assembly == null) return; // CrewStats nicht installiert
        CrewStatsLoaded = true;

        var patched = 0;
        foreach (var (typeName, methodName) in CrewStatsMethods)
        {
            var type = assembly.GetType(typeName);
            var method = type == null ? null : AccessTools.Method(type, methodName);
            if (method == null)
            {
                TestLabPlugin.Logger.LogWarning($"CrewStats-Schutz: {typeName}.{methodName} nicht gefunden.");
                continue;
            }
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(CompatGuard), nameof(Prefix)));
            patched++;
        }
        if (patched < CrewStatsMethods.Length)
            Warning = $"CrewStats-Schutz unvollständig ({patched}/{CrewStatsMethods.Length}) – Testrunden könnten an Discord gehen!";
        TestLabPlugin.Logger.LogInfo($"CrewStats gefunden: {patched}/{CrewStatsMethods.Length} Stellen für Testrunden abgeschaltet.");
    }

    /// <summary>
    /// Eine geladene Mod-DLL. Nicht über AccessTools.TypeByName: das durchsucht alle Assemblies und füllt das Log mit
    /// Warnungen über Spiel-Typen, die sich nicht laden lassen.
    /// </summary>
    internal static System.Reflection.Assembly? FindAssembly(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);

    // false = CrewStats-Methode überspringen
    private static bool Prefix()
    {
        if (!Blocking)
        {
            _loggedBlock = false;
            return true;
        }
        if (!_loggedBlock)
        {
            _loggedBlock = true;
            TestLabPlugin.Logger.LogInfo("Testrunde mit Bots: CrewStats zeichnet nichts auf und sendet nichts.");
        }
        return false;
    }
}
