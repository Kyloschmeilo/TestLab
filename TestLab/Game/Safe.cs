using System;
using System.Collections.Generic;

namespace TestLab.Game;

internal static class Safe
{
    // Fehler, die jeden Frame kommen würden, nur einmal ins Log schreiben
    private static readonly HashSet<string> Logged = new();

    /// <summary>Fehler der Mod dürfen das Spiel nie stören – nur ins Log (und als Hinweis ins Fenster).</summary>
    public static void Run(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exc)
        {
            if (!Logged.Add(what + "|" + exc.GetType().FullName + "|" + exc.Message)) return;
            TestLabPlugin.Logger.LogError($"TestLab-Fehler bei „{what}“ (Spiel läuft weiter): {exc}");
            Notice.Show($"Fehler bei „{what}“ – Details in BepInEx/LogOutput.log");
        }
    }
}

/// <summary>Kurze Rückmeldung, die das Fenster ein paar Sekunden unten anzeigt.</summary>
internal static class Notice
{
    public static string Text { get; private set; } = "";
    public static float Until { get; private set; }

    public static void Show(string text)
    {
        Text = text;
        Until = UnityEngine.Time.unscaledTime + 6f;
        TestLabPlugin.Logger.LogInfo(text);
    }
}
