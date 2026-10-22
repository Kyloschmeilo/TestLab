using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TestLab.Core;
using TestLab.Game;
using UnityEngine;

namespace TestLab.Ui;

/// <summary>
/// Das TestLab-Fenster (IMGUI, rechts oder links oben) und die Tasten. Nur GUI-/GUILayout-Funktionen, die Among Us selbst
/// enthält – der Rest ist im Spiel weggekürzt (es gibt z.B. kein TextField, keinen Slider und kein DragWindow).
/// Spielzustand wird in Update eingesammelt und Klicks erst im nächsten Update ausgeführt, damit sich zwischen
/// Layout- und Zeichen-Durchgang von OnGUI nichts ändert.
/// </summary>
public class TestLabWindow : MonoBehaviour
{
    public TestLabWindow(IntPtr ptr) : base(ptr)
    {
    }

    private sealed record Row(byte Id, string Name, Color Color, string Role, bool Impostor, bool Dead, bool IsBot, bool IsOwn,
        bool Controlled);

    private const int WindowId = 0x7E57AB;
    private const float Width = 540f;

    private static readonly Color Red = new(1f, 0.45f, 0.45f);
    private static readonly Color Green = new(0.55f, 1f, 0.55f);
    private static readonly Color Yellow = new(1f, 0.9f, 0.4f);
    private static readonly Color Gray = new(0.65f, 0.65f, 0.65f);
    private static readonly Color Backdrop = new(0.07f, 0.07f, 0.09f, 0.94f);

    private readonly Queue<Action> _clicks = new();
    private List<Row> _rows = new();
    private bool _active;
    private bool _lobby;
    private bool _game;
    private bool _meeting;
    private string _controlled = "";

    private GUI.WindowFunction? _draw;
    private Rect _rect = new(0f, 0f, Width, 10f);
    private static Rect _shown; // zuletzt gezeichnetes Fenster in Bildschirm-Pixeln (leer = keins)
    private Vector2 _scroll;
    private bool _guiErrorLogged;
    private GUIStyle? _backdrop; // einfarbiger Hintergrund (die Fenster-Grafik des Spiels ist fast durchsichtig)
    private GUIStyle? _noWrap; // Rollen nicht umbrechen, sonst wird die Zeile doppelt hoch
    private GUIContent? _empty;

    private void Update()
    {
        Safe.Run("Bots", Bots.Tick);
        Safe.Run("Bot-Abstimmung", BotVotes.Tick);
        while (_clicks.Count > 0) Safe.Run("Knopf", _clicks.Dequeue());
        Safe.Run("Tasten", Keys);
        Safe.Run("Fenster", Collect);
    }

    private static void Keys()
    {
        if (!Session.IsLocalHost) return;
        if (Input.GetKeyDown(KeyCode.F1)) Settings.ShowWindow.Value = !Settings.ShowWindow.Value;
        if (Input.GetKeyDown(KeyCode.F2)) Control.Next();
        if (Input.GetKeyDown(KeyCode.F3)) Control.Back();
        if (Input.GetKeyDown(KeyCode.F5)) Bots.Add(1);
        if (Input.GetKeyDown(KeyCode.F6)) Actions.BringAllBots();
    }

    private void Collect()
    {
        _active = Session.IsLocalHost;
        _lobby = Session.InLobby;
        _game = Session.InGame;
        _meeting = Session.InMeeting;
        var rows = new List<Row>();
        if (_active)
        {
            var local = PlayerControl.LocalPlayer;
            _controlled = local != null && local.Data != null ? local.Data.PlayerName : "";
            foreach (var player in Players.All().Where(Players.IsPresent).OrderBy(p => p.PlayerId))
            {
                var data = player.Data;
                var role = "";
                if (_game)
                {
                    role = RoleName(data.RoleType);
                    var (special, flash) = ModInfo.SpecialRole(player.PlayerId);
                    // „Crewmate · Sheriff“ → „Sheriff“; Spiel-Rollen mit eigener Fähigkeit bleiben stehen
                    if (special.Length > 0)
                        role = data.RoleType is RoleTypes.Crewmate or RoleTypes.Impostor ? special : role + " · " + special;
                    if (flash) role += " + Flash";
                }
                rows.Add(new Row(player.PlayerId, data.PlayerName ?? "?", PlayerColor(data.DefaultOutfit.ColorId), role,
                    _game && RoleManager.IsImpostorRole(data.RoleType), data.IsDead, Bots.IsBot(player), Control.IsOwn(player),
                    player == local));
            }
        }
        _rows = rows;
    }

    /// <summary>Maus über dem Fenster: dann bekommt das Spiel den Klick nicht (siehe ClickThroughPatch).</summary>
    public static bool MouseOverWindow
    {
        get
        {
            if (_shown.width <= 0f) return false;
            var mouse = Input.mousePosition;
            return _shown.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }
    }

    private void OnGUI()
    {
        if (!_active)
        {
            _shown = default;
            return;
        }
        try
        {
            var scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var screenWidth = Screen.width / scale;
            if (!Settings.ShowWindow.Value)
            {
                _shown = default;
                GUI.Label(new Rect(screenWidth - 260f, 8f, 120f, 24f), "TestLab: F1");
                return;
            }
            _draw ??= (Action<int>)Draw;
            _noWrap ??= new GUIStyle(GUI.skin.label) { wordWrap = false };
            _empty ??= new GUIContent("");
            if (_backdrop == null)
            {
                _backdrop = new GUIStyle();
                _backdrop.normal.background = Texture2D.whiteTexture; // eingefärbt über GUI.backgroundColor
            }
            // rechts neben den Spiel-Knöpfen am Rand oder links; Fenster werden nach allem anderen gezeichnet,
            // der Hintergrund liegt also darunter
            var x = Settings.WindowLeft.Value ? 20f : screenWidth - Width - 120f;
            var at = new Rect(x, 120f, Width, Mathf.Max(_rect.height, 10f));
            var background = GUI.backgroundColor;
            GUI.backgroundColor = Backdrop;
            GUI.Box(at, _empty, _backdrop);
            GUI.backgroundColor = background;
            _rect = GUILayout.Window(WindowId, new Rect(at.x, at.y, Width, 10f), _draw, $"TestLab {TestLabPlugin.Version}",
                Options(GUILayout.Width(Width)));
            _shown = new Rect(_rect.x * scale, _rect.y * scale, _rect.width * scale, _rect.height * scale);
        }
        catch (Exception exc)
        {
            if (_guiErrorLogged) return;
            _guiErrorLogged = true;
            TestLabPlugin.Logger.LogError($"TestLab-Fenster: {exc}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            DrawContent();
        }
        catch (Exception exc)
        {
            if (_guiErrorLogged) return;
            _guiErrorLogged = true;
            TestLabPlugin.Logger.LogError($"TestLab-Fenster: {exc}");
        }
    }

    private void DrawContent()
    {
        if (CompatGuard.Warning.Length > 0) ColoredLabel(CompatGuard.Warning, Red);
        var status = $"Lokales Spiel · {_rows.Count} Spieler · {Bots.Count} Bots";
        if (CompatGuard.Blocking) status += " · CrewStats sendet nichts";
        GUILayout.BeginHorizontal(Options());
        Label(status);
        GUILayout.FlexibleSpace();
        var left = Settings.WindowLeft.Value;
        if (Button(left ? "▶" : "◀", 28f)) _clicks.Enqueue(() => Settings.WindowLeft.Value = !left);
        GUILayout.EndHorizontal();

        if (_lobby) DrawLobby();
        else if (_game) DrawGame();
        else ColoredLabel("Warte auf Lobby oder Runde …", Gray);

        DrawPlayers();

        ColoredLabel("F1 Fenster · F2 nächster Spieler · F3 zu mir · F5 Bot · F6 Bots holen", Gray);
        if (Time.unscaledTime < Notice.Until) ColoredLabel(Notice.Text, Yellow);
    }

    private void DrawLobby()
    {
        GUILayout.BeginHorizontal(Options());
        if (Button("+ Bot")) _clicks.Enqueue(() => Bots.Add(1));
        if (Button($"Auffüllen bis {Settings.FillTo.Value}")) _clicks.Enqueue(() => Bots.Add(Bots.MissingForFill));
        if (Button("Alle entfernen")) _clicks.Enqueue(Bots.RemoveAll);
        GUILayout.EndHorizontal();
        Toggle(Settings.KeepBots.Value, "Bots nach der Runde behalten", v => Settings.KeepBots.Value = v);
        Toggle(Settings.QuickStart.Value, "Sofort starten (kein Countdown)", v => Settings.QuickStart.Value = v);
        ColoredLabel("Start wie immer über „Start“. Tipp: Diskussionszeit in den Lobby-Einstellungen auf 0.", Gray);
    }

    private void DrawGame()
    {
        ColoredLabel($"Du steuerst: {_controlled}", Green);
        if (_meeting) ColoredLabel("Meeting: Bots stimmen ab, wenn die Abstimmung offen ist.", Gray);

        GUILayout.BeginHorizontal(Options());
        if (Button("Zu mir (F3)")) _clicks.Enqueue(Control.Back);
        if (Button("Nächster (F2)")) _clicks.Enqueue(Control.Next);
        if (Button("Bots holen (F6)")) _clicks.Enqueue(Actions.BringAllBots);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal(Options());
        if (Button("Meeting")) _clicks.Enqueue(Actions.CallMeeting);
        if (Button("Crew gewinnt")) _clicks.Enqueue(() => Actions.EndRound(true));
        if (Button("Impostor gewinnen")) _clicks.Enqueue(() => Actions.EndRound(false));
        GUILayout.EndHorizontal();

        Toggle(Settings.NoAutoEnd.Value, "Kein automatisches Spielende", v => Settings.NoAutoEnd.Value = v);

        GUILayout.BeginHorizontal(Options());
        Label("Bots stimmen:");
        foreach (var mode in new[] { VoteMode.Skip, VoteMode.Zufall, VoteMode.Keine })
        {
            var text = mode == Settings.Votes.Value ? $"[{mode}]" : mode.ToString();
            if (Button(text)) _clicks.Enqueue(() => Settings.Votes.Value = mode);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawPlayers()
    {
        if (_rows.Count == 0) return;
        var height = Mathf.Min(_rows.Count * 27f + 8f, 260f);
        _scroll = GUILayout.BeginScrollView(_scroll, Options(GUILayout.Height(height)));
        foreach (var row in _rows)
        {
            GUILayout.BeginHorizontal(Options());
            ColoredLabel("●", row.Color, 16f);
            var name = (row.Controlled ? "▶ " : "") + row.Name + (row.IsOwn ? " (du)" : "") + (row.Dead ? " †" : "");
            ColoredLabel(name, row.Dead ? Gray : Color.white, 115f, _noWrap);
            ColoredLabel(row.Role, row.Impostor ? Red : Gray, 165f, _noWrap);
            var id = row.Id;
            if (_game && !_meeting)
            {
                if (!row.Controlled && Button("Steuern", 62f)) _clicks.Enqueue(() => WithPlayer(id, Control.SwitchTo));
                if (!row.Controlled && Button("Holen", 50f)) _clicks.Enqueue(() => WithPlayer(id, Actions.Bring));
                if (!row.Dead && Button("Töten", 50f)) _clicks.Enqueue(() => WithPlayer(id, Actions.Kill));
            }
            else if (_lobby && row.IsBot)
            {
                if (Button("Entfernen", 80f)) _clicks.Enqueue(() => WithPlayer(id, Bots.Remove));
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private static void WithPlayer(byte id, Action<PlayerControl> action)
    {
        var player = Players.ById(id);
        if (player != null) action(player);
    }

    private static string RoleName(RoleTypes role) => role switch
    {
        RoleTypes.CrewmateGhost => "Geist",
        RoleTypes.ImpostorGhost => "Geist (Impostor)",
        RoleTypes.GuardianAngel => "Guardian Angel",
        RoleTypes.SpiritGuide => "Influencer",
        _ => role.ToString(),
    };

    private static Color PlayerColor(int colorId)
    {
        var colors = Palette.PlayerColors;
        return colorId >= 0 && colorId < colors.Length ? colors[colorId] : Color.white;
    }

    // --- kleine IMGUI-Helfer (das Spiel will Optionen immer als Il2Cpp-Array) ---

    private static Il2CppReferenceArray<GUILayoutOption> Options(params GUILayoutOption[] options) => new(options);

    private static void Label(string text) => GUILayout.Label(text, Options());

    private static void ColoredLabel(string text, Color color, float width = 0f, GUIStyle? style = null)
    {
        var before = GUI.contentColor;
        GUI.contentColor = color;
        var options = width > 0f ? Options(GUILayout.Width(width)) : Options();
        if (style != null) GUILayout.Label(text, style, options);
        else GUILayout.Label(text, options);
        GUI.contentColor = before;
    }

    private static bool Button(string text, float width = 0f) =>
        GUILayout.Button(text, width > 0f ? Options(GUILayout.Width(width)) : Options());

    private static void Toggle(bool value, string text, Action<bool> set)
    {
        var now = GUILayout.Toggle(value, text, Options());
        if (now != value) set(now);
    }
}
