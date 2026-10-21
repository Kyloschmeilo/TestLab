using HarmonyLib;

namespace TestLab.Game;

// Alle Patches sind einfache Signaturen ohne Il2Cpp-Listen/Nullable (das hat bei CrewStats 1.3.3 das Spiel abstürzen
// lassen) und tun nur in lokalen Spielen mit Bots etwas.

/// <summary>Sofortstart: kein Countdown, wenn Bots in der Lobby sind.</summary>
[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
internal static class QuickStartPatch
{
    public static void Prefix(GameStartManager __instance)
    {
        if (Settings.QuickStart.Value && Session.IsTestGame && Session.IsLocalHost) __instance.countDownTimer = 0f;
    }
}

/// <summary>„Kein automatisches Spielende“: Siegbedingungen (Kills, Rauswurf, Sabotage) nicht prüfen.</summary>
[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class EndCriteriaPatch
{
    public static bool Prefix() => !AutoEndBlocked;

    internal static bool AutoEndBlocked => Settings.NoAutoEnd.Value && Session.IsTestGame && Session.IsLocalHost;
}

/// <summary>… und auch nicht „alle Aufgaben erledigt“.</summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
internal static class TaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!EndCriteriaPatch.AutoEndBlocked) return true;
        __result = false;
        return false;
    }
}

/// <summary>Rundenende: Steuerung zurück zum eigenen Spieler, bevor das Spiel den Endbildschirm baut.</summary>
[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class GameEndPatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix() => Safe.Run("Rundenende", Control.Restore);
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
internal static class ExitGamePatch
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix()
    {
        Safe.Run("Spiel verlassen", Control.Restore);
        Bots.Clear();
    }
}

/// <summary>Klicks auf das TestLab-Fenster nicht auch an die Spiel-Knöpfe darunter geben.</summary>
[HarmonyPatch(typeof(PassiveButtonManager), nameof(PassiveButtonManager.Update))]
internal static class ClickThroughPatch
{
    public static bool Prefix() => !Ui.TestLabWindow.MouseOverWindow;
}
