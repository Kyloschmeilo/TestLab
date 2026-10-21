using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TestLab.Core;
using UnityEngine;

namespace TestLab.Game;

/// <summary>
/// Im Meeting stimmen alle ab, die du gerade nicht steuerst (Bots und dein eigener Spieler, falls du einen Bot steuerst),
/// sobald die Abstimmung offen ist – nacheinander mit kurzem Abstand. Läuft über die normale Host-Abstimmung des
/// Spiels, also auch mit Mods, die Stimmen prüfen (z.B. CrewStats „Kein Skip bei Notfall“: abgelehnter Skip →
/// der Bot wählt danach einen zufälligen Spieler).
/// </summary>
internal static class BotVotes
{
    private const float FirstDelay = 1f;
    private const float Spacing = 0.4f;
    private const float RetryDelay = 1f;

    private static IntPtr _meeting;
    private static float _openSince = -1f;
    private static readonly Dictionary<byte, int> Attempts = new();
    private static readonly Dictionary<byte, float> NextTry = new();

    public static void Tick()
    {
        var meeting = MeetingHud.Instance;
        if (meeting == null || !Session.IsLocalHost || !Session.IsTestGame)
        {
            _meeting = IntPtr.Zero;
            return;
        }
        if (meeting.Pointer != _meeting)
        {
            _meeting = meeting.Pointer;
            _openSince = -1f;
            Attempts.Clear();
            NextTry.Clear();
        }
        if (meeting.state is not (MeetingHud.MeetingStates.NotVoted or MeetingHud.MeetingStates.Voted)) return;
        if (_openSince < 0f) _openSince = Time.unscaledTime;

        var mode = Settings.Votes.Value;
        if (mode == VoteMode.Keine) return;

        var local = PlayerControl.LocalPlayer;
        var alive = Players.All().Where(Players.IsAlive).Select(p => p.PlayerId).ToList();
        var now = Time.unscaledTime;
        var index = 0;
        foreach (var area in meeting.playerStates)
        {
            if (area == null) continue;
            var voter = (byte)area.PlayerId;
            index++;
            if (area.AmDead || area.DidVote) continue;
            if (local != null && voter == local.PlayerId) continue; // den steuerst du gerade selbst
            if (!Players.IsAlive(Players.ById(voter))) continue;
            if (now < _openSince + FirstDelay + index * Spacing) continue;
            if (NextTry.TryGetValue(voter, out var next) && now < next) continue;

            Attempts.TryGetValue(voter, out var attempt);
            var choice = VotePlanner.Choose(mode, voter, alive, attempt, RandomNumberGenerator.GetInt32);
            Attempts[voter] = attempt + 1;
            NextTry[voter] = now + RetryDelay;
            if (!choice.Vote) continue;
            meeting.CmdCastVote(voter, choice.Target ?? PlayerVoteArea.SkippedVote);
        }
    }
}
