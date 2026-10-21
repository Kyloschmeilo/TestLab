using System.Linq;
using TestLab.Core;
using UnityEngine;

namespace TestLab.Game;

/// <summary>
/// Steuern eines anderen Spielers (Bot oder du selbst): das Spiel hält ihn dann für den eigenen Spieler
/// (PlayerControl.LocalPlayer, eigene Client-ID), Kamera, Licht, Knöpfe und Aufgaben wechseln mit. Die Host-ID
/// wechselt mit, damit Host-Logik (Kills, Meetings, andere Mods) weiterläuft. Zum Rundenende geht alles zurück.
/// </summary>
internal static class Control
{
    private const byte None = 255;

    private static int _ownClientId = -1;
    private static byte _ownPlayerId = None;

    public static bool IsSwitched => _ownClientId >= 0;

    /// <summary>Dein echter Spieler (auch wenn du gerade einen Bot steuerst).</summary>
    public static PlayerControl? Own => _ownPlayerId == None ? PlayerControl.LocalPlayer : Players.ById(_ownPlayerId);

    public static bool IsOwn(PlayerControl player) =>
        _ownPlayerId == None ? player == PlayerControl.LocalPlayer : player.PlayerId == _ownPlayerId;

    /// <summary>Warum gerade nicht gewechselt werden kann (null = geht).</summary>
    public static string? WhyNot(PlayerControl? target)
    {
        if (!Session.InGame) return "Steuern geht nur während einer Runde.";
        if (Session.InMeeting) return "Im Meeting kann nicht gewechselt werden.";
        if (target == null || !Players.IsPresent(target)) return "Den Spieler gibt es nicht mehr.";
        if (target == PlayerControl.LocalPlayer) return null;
        if (PlayerControl.LocalPlayer == null || HudManager.Instance == null) return "Das Spiel ist noch nicht bereit.";
        return null;
    }

    public static void SwitchTo(PlayerControl target)
    {
        var why = WhyNot(target);
        if (why != null)
        {
            Notice.Show(why);
            return;
        }
        var old = PlayerControl.LocalPlayer;
        if (target == old) return;

        var client = AmongUsClient.Instance;
        var hud = HudManager.Instance;
        if (_ownClientId < 0)
        {
            _ownClientId = client.ClientId;
            _ownPlayerId = old.PlayerId;
        }

        if (Minigame.Instance != null) Safe.Run("Aufgabe schließen", () => Minigame.Instance.Close());

        // Alten Spieler dort stehen lassen, wo er ist (sonst läuft er zur letzten Netzwerk-Position zurück)
        var light = old.lightSource;
        Safe.Run("alten Spieler anhalten", () =>
        {
            old.NetTransform.RpcSnapTo(old.transform.position);
            old.moveable = false;
            ResetName(old);
        });

        var killTimer = target.killTimer;
        PlayerControl.LocalPlayer = target;
        client.ClientId = target.OwnerId;
        client.HostId = target.OwnerId;

        target.lightSource = light;
        target.moveable = true;
        if (light != null)
        {
            light.transform.SetParent(target.transform);
            light.transform.localPosition = target.Collider.offset;
        }
        Safe.Run("Kamera", () => Camera.main.GetComponent<FollowerCamera>().SetTarget(target));
        Safe.Run("Bewegung", () =>
        {
            target.MyPhysics.ResetMoveState(true);
            KillAnimation.SetMovement(target, true);
            if (target.MyPhysics.inputHandler != null) target.MyPhysics.inputHandler.enabled = true;
        });

        Safe.Run("Knöpfe", () =>
        {
            HideExtraKillButtons(hud);
            var role = target.Data.Role;
            if (role != null)
            {
                role.Initialize(target); // Kill-/Fähigkeits-Knöpfe der neuen Rolle (setzt auch den Kill-Cooldown)
                target.SetKillTimer(killTimer);
            }
            else
            {
                hud.SetHudActive(true);
            }
            if (hud.ShadowQuad != null) hud.ShadowQuad.gameObject.SetActive(!target.Data.IsDead);
        });

        Notice.Show($"Du steuerst jetzt {target.Data.PlayerName}.");
    }

    public static void Next()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null) return;
        var next = Roster.Next(Players.All().Where(Players.IsPresent).Select(p => p.PlayerId), local.PlayerId);
        var target = next == null ? null : Players.ById(next.Value);
        if (target != null) SwitchTo(target);
    }

    public static void Back()
    {
        var own = Own;
        if (own == null || own == PlayerControl.LocalPlayer) return;
        SwitchTo(own);
    }

    /// <summary>Rundenende / Spiel verlassen: Client-ID und eigenen Spieler zurück (vor allem anderen des Spiels).</summary>
    public static void Restore()
    {
        if (_ownClientId < 0) return;
        var client = AmongUsClient.Instance;
        if (client != null)
        {
            client.ClientId = _ownClientId;
            client.HostId = _ownClientId;
        }
        var own = Players.ById(_ownPlayerId);
        var current = PlayerControl.LocalPlayer;
        if (own != null)
        {
            if (current != null && current != own) Safe.Run("Name zurücksetzen", () => ResetName(current));
            PlayerControl.LocalPlayer = own;
            own.moveable = true;
        }
        _ownClientId = -1;
        _ownPlayerId = None;
        TestLabPlugin.Logger.LogInfo("Steuerung zurück beim eigenen Spieler.");
    }

    /// <summary>
    /// Mods (z.B. SpecialRoles) bauen eigene Knöpfe als Kopie des Kill-Knopfs und blenden sie nur für die eigene Rolle
    /// ein und aus. Nach dem Wechsel stünde der Knopf der alten Rolle sonst weiter da.
    /// </summary>
    private static void HideExtraKillButtons(HudManager hud)
    {
        var original = hud.KillButton;
        if (original == null || original.transform.parent == null) return;
        foreach (var button in original.transform.parent.GetComponentsInChildren<KillButton>(true))
            if (button != null && button != original)
                button.gameObject.SetActive(false);
    }

    /// <summary>Mods schreiben die eigene Rolle unter den Namen – beim alten Spieler wieder nur den Namen zeigen.</summary>
    private static void ResetName(PlayerControl player)
    {
        var text = player.cosmetics?.nameText;
        if (text != null && player.Data != null) text.text = player.Data.PlayerName;
    }
}
