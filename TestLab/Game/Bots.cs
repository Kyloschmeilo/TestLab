using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using InnerNet;
using TestLab.Core;
using UnityEngine;

namespace TestLab.Game;

/// <summary>
/// Bots = Spieler ohne eigenes Spiel: der Host legt für sie einen Client an (Client-ID ab 100, wie ein Mitspieler, der
/// beigetreten ist) und lässt das Spiel ihren Spieler erzeugen. Alles läuft beim Host; im lokalen Spiel prüft kein
/// Server, ob es den Client wirklich gibt. Bots bewegen sich nicht selbst – du steuerst sie über das Fenster.
/// </summary>
internal static class Bots
{
    private const float RetrySeconds = 3f;
    private const int MaxFailures = 5;

    private sealed class Bot
    {
        public int ClientId;
        public int Number;
        public string Name = "";
        public byte Color;
        public IntPtr SetUpFor; // Spieler-Objekt, für das Name und Farbe schon gesetzt sind
        public float RetryAt;
        public int Failures;
    }

    private static readonly List<Bot> List = new();
    private static float _lobbySince = -1f;
    private static float _notJoinedSince = -1f;

    public static int Count => List.Count;

    public static bool IsBot(PlayerControl? player) => player != null && IsBotClient(player.OwnerId);

    public static bool IsBotClient(int clientId) => List.Exists(b => b.ClientId == clientId);

    public static int FreeSlots
    {
        get
        {
            var client = AmongUsClient.Instance;
            return client == null ? 0 : Math.Max(0, MaxPlayers() - client.allClients.Count);
        }
    }

    public static int MissingForFill
    {
        get
        {
            var client = AmongUsClient.Instance;
            return client == null ? 0 : Roster.MissingForFill(client.allClients.Count, Settings.FillTo.Value, MaxPlayers());
        }
    }

    public static void Add(int howMany)
    {
        if (!Session.InLobby)
        {
            Notice.Show("Bots gehen nur in der Lobby eines lokalen Spiels (Lokal → Spiel erstellen).");
            return;
        }
        var count = Math.Min(howMany, FreeSlots);
        if (count <= 0)
        {
            Notice.Show("Die Lobby ist voll – in den Lobby-Einstellungen mehr Spieler erlauben.");
            return;
        }
        for (var i = 0; i < count; i++)
        {
            var bot = new Bot
            {
                ClientId = Roster.NextClientId(AmongUsClient.Instance.allClients.ToArray().Select(c => c.Id)
                    .Concat(List.Select(b => b.ClientId))),
                Number = Roster.NextNumber(List.Select(b => b.Number)),
                Color = (byte)RandomNumberGenerator.GetInt32(Palette.PlayerColors.Length),
            };
            bot.Name = $"Bot {bot.Number}";
            List.Add(bot);
            Spawn(bot);
        }
        Notice.Show(count == 1 ? "1 Bot hinzugefügt." : $"{count} Bots hinzugefügt.");
    }

    public static void Remove(PlayerControl player)
    {
        var bot = List.FirstOrDefault(b => b.ClientId == player.OwnerId);
        if (bot == null) return;
        if (!Session.InLobby)
        {
            Notice.Show("Bots lassen sich nur in der Lobby entfernen.");
            return;
        }
        RemoveFromGame(bot);
        Notice.Show($"{bot.Name} entfernt.");
    }

    public static void RemoveAll()
    {
        if (!Session.InLobby)
        {
            Notice.Show("Bots lassen sich nur in der Lobby entfernen.");
            return;
        }
        var count = List.Count;
        foreach (var bot in List.ToList()) RemoveFromGame(bot);
        Notice.Show($"{count} Bots entfernt.");
    }

    /// <summary>Läuft jeden Frame: Bots bereit halten, nach einer Runde wieder in die Lobby holen.</summary>
    public static void Tick()
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.NetworkMode != NetworkModes.LocalGame)
        {
            Clear();
            return;
        }
        if (client.GameState == InnerNetClient.GameStates.NotJoined)
        {
            // kurz „nicht verbunden“ kann auch der Weg zurück in die Lobby sein; länger = Spiel verlassen
            if (_notJoinedSince < 0f) _notJoinedSince = Time.unscaledTime;
            else if (Time.unscaledTime - _notJoinedSince > 15f) Clear();
            return;
        }
        _notJoinedSince = -1f;

        // Beim Rundenstart wartet der Host, bis alle Clients „bereit“ sind, und wirft sonst nach ein paar Sekunden raus
        if (client.GameState is InnerNetClient.GameStates.Joined or InnerNetClient.GameStates.Started)
        {
            foreach (var bot in List)
            {
                var data = FindClient(bot.ClientId);
                if (data == null) continue;
                data.IsReady = true;
                data.InScene = true;
            }
        }

        if (!Session.InLobby || PlayerControl.LocalPlayer == null || GameData.Instance == null)
        {
            _lobbySince = -1f;
            return;
        }
        if (_lobbySince < 0f) _lobbySince = Time.unscaledTime;

        foreach (var bot in List.ToList())
        {
            var character = FindClient(bot.ClientId)?.Character;
            if (character != null && character.Data != null)
            {
                if (bot.SetUpFor != character.Pointer) SetUp(bot, character);
                continue;
            }

            // Bot fehlt: nach einer Runde (das Spiel holt nur echte Clients zurück) oder Erzeugen hat nicht geklappt
            if (bot.SetUpFor != IntPtr.Zero && !Settings.KeepBots.Value)
            {
                Forget(bot);
                continue;
            }
            if (Time.unscaledTime < bot.RetryAt || Time.unscaledTime - _lobbySince < 1.5f) continue;
            if (bot.Failures >= MaxFailures)
            {
                Forget(bot);
                Notice.Show($"{bot.Name} ließ sich nicht erzeugen und wurde entfernt.");
                continue;
            }
            bot.Failures++;
            Spawn(bot);
        }
    }

    /// <summary>Spiel verlassen: die Bots gibt es nicht mehr (ihre Clients räumt das Spiel mit auf).</summary>
    public static void Clear()
    {
        if (List.Count > 0) TestLabPlugin.Logger.LogInfo($"Spiel verlassen – {List.Count} Bots vergessen.");
        List.Clear();
        _lobbySince = -1f;
        _notJoinedSince = -1f;
    }

    private static void Spawn(Bot bot)
    {
        var client = AmongUsClient.Instance;
        var data = FindClient(bot.ClientId);
        if (data == null)
        {
            var platform = new PlatformSpecificData { Platform = Platforms.StandaloneWin10, PlatformName = "TestLab" };
            var created = new ClientData(bot.ClientId, bot.Name, platform, 1u, "", "");
            WithClientsLocked(() => client.allClients.Add(created));
            data = created;
        }
        data.IsReady = true;
        data.InScene = true;
        data.IsBeingCreated = false; // sonst bricht CreatePlayer sofort ab, falls ein früherer Versuch hängen blieb
        bot.RetryAt = Time.unscaledTime + RetrySeconds;
        // Im lokalen Spiel erzeugt der Host den Spieler sofort (ohne Warten auf den Server)
        client.StartCoroutine(client.CreatePlayer(data));
    }

    private static void SetUp(Bot bot, PlayerControl character)
    {
        bot.SetUpFor = character.Pointer;
        bot.Failures = 0;
        character.RpcSetName(bot.Name);
        character.CheckColor(bot.Color); // nimmt die nächste freie Farbe, falls vergeben (wie bei echten Spielern)
        TestLabPlugin.Logger.LogInfo($"{bot.Name} ist in der Lobby (Client {bot.ClientId}, Spieler {character.PlayerId}).");
    }

    private static void RemoveFromGame(Bot bot)
    {
        List.Remove(bot);
        var client = AmongUsClient.Instance;
        if (client == null) return;
        var data = FindClient(bot.ClientId);
        // wie ein Spieler, der die Lobby verlässt (entfernt auch den Client aus allClients)
        if (data?.Character != null) client.RemovePlayer(bot.ClientId, DisconnectReasons.ExitGame);
        Forget(bot);
    }

    private static void Forget(Bot bot)
    {
        List.Remove(bot);
        var data = FindClient(bot.ClientId);
        if (data != null) WithClientsLocked(() => AmongUsClient.Instance.allClients.Remove(data));
    }

    /// <summary>Das Spiel liest allClients auch vom Netzwerk-Thread – Änderungen wie das Spiel unter Lock.</summary>
    private static void WithClientsLocked(Action action)
    {
        var clients = AmongUsClient.Instance.allClients;
        var taken = false;
        try
        {
            Il2CppSystem.Threading.Monitor.Enter(clients, ref taken);
            action();
        }
        finally
        {
            if (taken) Il2CppSystem.Threading.Monitor.Exit(clients);
        }
    }

    private static ClientData? FindClient(int clientId)
    {
        var client = AmongUsClient.Instance;
        if (client == null) return null;
        foreach (var data in client.allClients)
            if (data != null && data.Id == clientId)
                return data;
        return null;
    }

    private static int MaxPlayers()
    {
        var options = GameOptionsManager.Instance?.CurrentGameOptions;
        return Math.Clamp(options?.MaxPlayers ?? 15, 1, 15);
    }
}
