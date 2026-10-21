using System.Collections.Generic;
using InnerNet;

namespace TestLab.Game;

/// <summary>Wann TestLab überhaupt etwas tut: nur in lokalen Spielen (Lokal → Spiel erstellen) als Host.</summary>
internal static class Session
{
    public static bool IsLocalHost
    {
        get
        {
            var client = AmongUsClient.Instance;
            // AmHost bleibt auch beim Steuern eines Bots wahr (HostId wechselt mit, siehe Control)
            return client != null && client.NetworkMode == NetworkModes.LocalGame && client.AmHost
                && client.GameState is InnerNetClient.GameStates.Joined or InnerNetClient.GameStates.Started;
        }
    }

    public static bool InLobby =>
        IsLocalHost && LobbyBehaviour.Instance != null && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;

    public static bool InGame =>
        IsLocalHost && ShipStatus.Instance != null && AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;

    public static bool InMeeting => MeetingHud.Instance != null || ExileController.Instance != null;

    /// <summary>Lokales Spiel mit Bots: hier gelten Testregeln (CrewStats aus, Sofortstart, …).</summary>
    public static bool IsTestGame
    {
        get
        {
            var client = AmongUsClient.Instance;
            return client != null && client.NetworkMode == NetworkModes.LocalGame && Bots.Count > 0;
        }
    }
}

internal static class Players
{
    public static PlayerControl? ById(byte playerId)
    {
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player != null && player.PlayerId == playerId)
                return player;
        return null;
    }

    public static IEnumerable<PlayerControl> All()
    {
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player != null && player.Data != null)
                yield return player;
    }

    public static bool IsAlive(PlayerControl? player) =>
        player != null && player.Data != null && !player.Data.IsDead && !player.Data.Disconnected;

    public static bool IsPresent(PlayerControl? player) => player != null && player.Data != null && !player.Data.Disconnected;
}
