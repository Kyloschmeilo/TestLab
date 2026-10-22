using AmongUs.GameOptions;

namespace TestLab.Game;

/// <summary>
/// Rolle mitten in der Runde wechseln (Fenster → „Rolle ändern“), für den Spieler, den du gerade steuerst.
/// Spiel-Rolle über RoleManager.SetRole – das macht das Spiel bei der Rollenvergabe auch (alte Rolle weg, neue
/// Knöpfe/Sicht). Bewusst nicht RpcSetRole: das zählt Rollen-Statistik in deinem Profil und kann das Intro neu
/// starten. Gilt nur bei dir (Host); im lokalen Spiel mit Bots reicht das, echte Mitspieler bekämen es nicht mit.
/// </summary>
internal static class RoleSwitch
{
    public static readonly RoleTypes[] CrewRoles =
    {
        RoleTypes.Crewmate, RoleTypes.Engineer, RoleTypes.Scientist, RoleTypes.Tracker,
        RoleTypes.Noisemaker, RoleTypes.Detective, RoleTypes.Judge, RoleTypes.SpiritGuide,
    };

    public static readonly RoleTypes[] ImpostorRoles =
    {
        RoleTypes.Impostor, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper,
    };

    /// <summary>Warum die Rolle gerade nicht geändert werden kann (null = geht).</summary>
    public static string? WhyNot(PlayerControl? player)
    {
        if (!Session.InGame) return "Rollen lassen sich nur während einer Runde ändern.";
        if (Session.InMeeting) return "Im Meeting geht das nicht.";
        if (player == null || player.Data == null || player.Data.Disconnected) return "Den Spieler gibt es nicht mehr.";
        if (player.Data.IsDead) return "Tote behalten ihre Geister-Rolle.";
        if (RoleManager.Instance == null) return "Das Spiel ist noch nicht bereit.";
        return null;
    }

    /// <summary>Spiel-Rolle setzen. Eine SpecialRoles-Rolle fällt dabei weg (Flash bleibt).</summary>
    public static void SetGameRole(PlayerControl player, RoleTypes role)
    {
        if (!Check(player)) return;
        var id = player.PlayerId;
        if (ModInfo.SpecialRoleId(id) != 0) ModInfo.SetSpecialRole(id, 0, ModInfo.HasFlash(id));
        Apply(player, role);
        Done(player, Name(role));
    }

    /// <summary>SpecialRoles-Rolle setzen. Die gibt es nur auf Crewmate bzw. Impostor – die Spiel-Rolle wechselt mit.</summary>
    public static void SetModRole(PlayerControl player, ModInfo.ModRole role)
    {
        if (!Check(player)) return;
        var baseRole = role.Impostor ? RoleTypes.Impostor : RoleTypes.Crewmate;
        if (player.Data.RoleType != baseRole) Apply(player, baseRole);
        if (!ModInfo.SetSpecialRole(player.PlayerId, role.Id, ModInfo.HasFlash(player.PlayerId)))
        {
            Notice.Show("Die SpecialRoles-Rolle ließ sich nicht setzen – Details im Log.");
            return;
        }
        RefreshLocal(player);
        Done(player, role.Name);
    }

    public static void ToggleFlash(PlayerControl player)
    {
        if (!Check(player)) return;
        var id = player.PlayerId;
        var flash = !ModInfo.HasFlash(id);
        if (!ModInfo.SetSpecialRole(id, ModInfo.SpecialRoleId(id), flash))
        {
            Notice.Show("Flash ließ sich nicht umschalten – Details im Log.");
            return;
        }
        Notice.Show(flash ? $"{player.Data.PlayerName} hat jetzt Flash." : $"{player.Data.PlayerName} hat kein Flash mehr.");
    }

    public static string Name(RoleTypes role) => role switch
    {
        RoleTypes.CrewmateGhost => "Geist",
        RoleTypes.ImpostorGhost => "Geist (Impostor)",
        RoleTypes.GuardianAngel => "Guardian Angel",
        RoleTypes.SpiritGuide => "Influencer",
        _ => role.ToString(),
    };

    private static bool Check(PlayerControl player)
    {
        var why = WhyNot(player);
        if (why == null) return true;
        Notice.Show(why);
        return false;
    }

    private static void Apply(PlayerControl player, RoleTypes role)
    {
        RoleManager.Instance.SetRole(player, role);
        // Aufgaben von Impostorn zählen nicht für den Crew-Sieg
        Safe.Run("Aufgaben neu zählen", () => GameData.Instance?.RecomputeTaskCounts());
        RefreshLocal(player);
    }

    private static void RefreshLocal(PlayerControl player)
    {
        if (player == PlayerControl.LocalPlayer) Safe.Run("Knöpfe", () => Control.RefreshButtons(player, null));
    }

    private static void Done(PlayerControl player, string role)
    {
        var text = $"{player.Data.PlayerName} ist jetzt {role}.";
        if (!Settings.NoAutoEnd.Value && RoundWouldEnd())
            text += " Damit endet die Runde – vorher „Kein automatisches Spielende“ anhaken.";
        Notice.Show(text);
    }

    /// <summary>Kein Impostor mehr oder so viele wie Crew: dann beendet das Spiel die Runde von selbst.</summary>
    private static bool RoundWouldEnd()
    {
        int impostors = 0, crew = 0;
        foreach (var player in Players.All())
        {
            if (!Players.IsAlive(player)) continue;
            if (RoleManager.IsImpostorRole(player.Data.RoleType)) impostors++;
            else crew++;
        }
        return impostors == 0 || impostors >= crew;
    }
}
