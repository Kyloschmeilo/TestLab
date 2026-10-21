using System.Linq;
using UnityEngine;

namespace TestLab.Game;

/// <summary>Knöpfe im Fenster während einer Runde. Alles läuft über die normalen Host-Funktionen des Spiels.</summary>
internal static class Actions
{
    /// <summary>Spieler stirbt sofort (Leiche bleibt liegen, wie bei einem Kill).</summary>
    public static void Kill(PlayerControl target)
    {
        if (!InRound() || !Players.IsAlive(target)) return;
        target.RpcMurderPlayer(target, true);
        Notice.Show($"{target.Data.PlayerName} getötet.");
    }

    /// <summary>Spieler zu dir holen (an deine Position).</summary>
    public static void Bring(PlayerControl target)
    {
        var local = PlayerControl.LocalPlayer;
        if (!InRound() || local == null || target == local || !Players.IsPresent(target)) return;
        target.NetTransform.RpcSnapTo(local.transform.position);
    }

    public static void BringAllBots()
    {
        var local = PlayerControl.LocalPlayer;
        if (!InRound() || local == null) return;
        var count = 0;
        foreach (var player in Players.All().Where(p => Bots.IsBot(p) && p != local && Players.IsAlive(p) && !p.inVent))
        {
            player.NetTransform.RpcSnapTo(local.transform.position);
            count++;
        }
        Notice.Show($"{count} Bots zu dir geholt.");
    }

    /// <summary>Notfall-Meeting, einberufen vom gesteuerten Spieler (oder dem nächsten lebenden).</summary>
    public static void CallMeeting()
    {
        if (!InRound()) return;
        var caller = Players.IsAlive(PlayerControl.LocalPlayer) ? PlayerControl.LocalPlayer : Players.All().FirstOrDefault(Players.IsAlive);
        if (caller == null) return;
        caller.ReportDeadBody(null); // Host-Seite von CmdReportDeadBody: prüft Sabotage, startet das Meeting für alle
    }

    public static void EndRound(bool crewWins)
    {
        if (!InRound() || GameManager.Instance == null) return;
        GameManager.Instance.RpcEndGame(crewWins ? GameOverReason.CrewmatesByVote : GameOverReason.ImpostorsByKill, false);
    }

    private static bool InRound()
    {
        if (Session.InGame && !Session.InMeeting) return true;
        if (Session.InGame && MeetingHud.Instance != null) Notice.Show("Erst das Meeting abwarten.");
        return false;
    }
}
