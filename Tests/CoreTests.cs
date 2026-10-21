using System;
using System.Collections.Generic;
using System.Linq;
using TestLab.Core;
using Xunit;

namespace TestLab.Tests;

public class VotePlannerTests
{
    private static readonly byte[] Alive = { 0, 1, 2, 3 };

    [Fact]
    public void SkipModeSkipsFirst()
    {
        var choice = VotePlanner.Choose(VoteMode.Skip, 1, Alive, 0, _ => 0);
        Assert.Equal(VoteChoice.SkipVote, choice);
    }

    [Fact]
    public void RejectedSkipFallsBackToAnotherPlayer()
    {
        // z.B. CrewStats „Kein Skip bei Notfall“ nimmt den Skip zurück → zweiter Versuch wählt einen Spieler
        for (var pick = 0; pick < 3; pick++)
        {
            var choice = VotePlanner.Choose(VoteMode.Skip, 1, Alive, 1, _ => pick);
            Assert.True(choice.Vote);
            Assert.NotNull(choice.Target);
            Assert.NotEqual((byte)1, choice.Target);
            Assert.Contains(choice.Target!.Value, Alive);
        }
    }

    [Fact]
    public void RandomNeverVotesForItself()
    {
        var seen = new HashSet<byte>();
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var choice = VotePlanner.Choose(VoteMode.Zufall, 2, Alive, 0, random.Next);
            Assert.True(choice.Vote);
            Assert.NotEqual((byte)2, choice.Target);
            seen.Add(choice.Target!.Value);
        }
        Assert.Equal(new byte[] { 0, 1, 3 }, seen.OrderBy(b => b));
    }

    [Fact]
    public void NoneModeAndTooManyAttemptsDoNotVote()
    {
        Assert.False(VotePlanner.Choose(VoteMode.Keine, 0, Alive, 0, _ => 0).Vote);
        Assert.False(VotePlanner.Choose(VoteMode.Zufall, 0, Alive, VotePlanner.MaxAttempts, _ => 0).Vote);
    }

    [Fact]
    public void AloneSkipsOnceThenGivesUp()
    {
        var alone = new byte[] { 5 };
        Assert.Equal(VoteChoice.SkipVote, VotePlanner.Choose(VoteMode.Zufall, 5, alone, 0, _ => 0));
        Assert.False(VotePlanner.Choose(VoteMode.Zufall, 5, alone, 1, _ => 0).Vote);
    }
}

public class RosterTests
{
    [Fact]
    public void ClientIdsStartAt100AndFillGaps()
    {
        Assert.Equal(100, Roster.NextClientId(new[] { 1, 2 }));
        Assert.Equal(102, Roster.NextClientId(new[] { 1, 100, 101 }));
        Assert.Equal(101, Roster.NextClientId(new[] { 100, 102 }));
    }

    [Fact]
    public void BotNumbersReuseFreedNumbers()
    {
        Assert.Equal(1, Roster.NextNumber(Array.Empty<int>()));
        Assert.Equal(2, Roster.NextNumber(new[] { 1, 3 }));
        Assert.Equal(4, Roster.NextNumber(new[] { 1, 2, 3 }));
    }

    [Theory]
    [InlineData(1, 8, 15, 7)]
    [InlineData(5, 8, 6, 1)] // Lobby-Größe begrenzt
    [InlineData(9, 8, 15, 0)] // schon genug
    public void FillUpToTargetWithinLobbySize(int players, int target, int max, int expected) =>
        Assert.Equal(expected, Roster.MissingForFill(players, target, max));

    [Fact]
    public void NextPlayerCyclesById()
    {
        var players = new byte[] { 4, 0, 2 };
        Assert.Equal((byte)2, Roster.Next(players, 0));
        Assert.Equal((byte)4, Roster.Next(players, 2));
        Assert.Equal((byte)0, Roster.Next(players, 4));
        Assert.Equal((byte)0, Roster.Next(players, 9)); // aktueller Spieler nicht (mehr) in der Liste
        Assert.Null(Roster.Next(new byte[] { 3 }, 3));
        Assert.Null(Roster.Next(Array.Empty<byte>(), 0));
    }
}
