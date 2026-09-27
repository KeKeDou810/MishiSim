using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class DeckDrawTests
{
    private NetworkTestMatch match;
    private long sequence;
    [SetUp] public void Setup()
    {
        var cards = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, 1000, true) }
            .Concat(Enumerable.Range(0, 49).Select(i => new NetworkTestMatch.Card(Guid.Empty, "unit" + i, 0, -1, 4000))).ToArray();
        match = NetworkTestMatch.FromDecks(new BattleBoard(), new[] { 0, 1, 2 }, 0, 1,
            new[] { cards, cards }, new MatchDrawRules(5, 1, new[] { TestTurnPhase.Rebuild, TestTurnPhase.TimeReset, TestTurnPhase.Combat }), new Random(7));
        sequence = 0;
    }
    private bool Next() => match.TryCommand(match.ActivePlayer, match.MatchId, ++sequence, match.Revision, TestCommandKind.NextPhase, Guid.Empty, -1, out _);
    [Test] public void InitialPileIsPrivateAndOpeningDealIsIdempotent()
    {
        var view = match.ForPlayer(0);
        CollectionAssert.AreEqual(new[] { 49, 49 }, view.DeckCounts);
        Assert.AreEqual(0, view.OwnHand.Length); Assert.AreEqual(2, view.Board.Length);
        Assert.IsFalse(Next());
        match.DealOpeningHands(); match.DealOpeningHands();
        view = match.ForPlayer(0);
        CollectionAssert.AreEqual(new[] { 44, 44 }, view.DeckCounts);
        Assert.AreEqual(5, view.OwnHand.Length); Assert.AreEqual(5, view.OpponentHandCount);
        Assert.AreEqual(0, view.PublicPiles.Length);
        Assert.IsFalse(view.OwnHand.Any(c => c.Owner == 1 || c.IsContract));
        Assert.AreEqual(10, view.OwnHand.Concat(match.ForPlayer(1).OwnHand).Select(c => c.Id).Distinct().Count());
    }
    [Test] public void FirstTurnDrawsOnceThenSkipsOnlyConfiguredPhases()
    {
        match.DealOpeningHands();
        Assert.IsTrue(Next()); Assert.AreEqual(TestTurnPhase.Draw, match.Phase);
        Assert.AreEqual(6, match.ForPlayer(0).OwnHand.Length); Assert.AreEqual(43, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsTrue(Next()); Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        Assert.IsTrue(Next()); Assert.AreEqual(TestTurnPhase.End, match.Phase);
        Assert.AreEqual(6, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(match.TryCommand(0, match.MatchId, ++sequence, match.Revision, TestCommandKind.EndTurn, Guid.Empty, -1, out _));
        foreach (var phase in new[] { TestTurnPhase.Draw, TestTurnPhase.Rebuild, TestTurnPhase.TimeReset, TestTurnPhase.Main, TestTurnPhase.Combat, TestTurnPhase.End })
        { Assert.IsTrue(Next()); Assert.AreEqual(phase, match.Phase); }
        Assert.AreEqual(6, match.ForPlayer(1).OwnHand.Length);
    }
    [Test] public void ConfigurationChangesDealAndDrawAndDecisionCannotSummon()
    {
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "c", 0, -1, 0, true),
            new NetworkTestMatch.Card(Guid.Empty, "decision", 0, -1, 0, false, TestCardZone.Deck, true),
            new NetworkTestMatch.Card(Guid.Empty, "decision", 0, -1, 0, false, TestCardZone.Deck, true) };
        match = NetworkTestMatch.FromDecks(new BattleBoard(), new[] { 0, 1, 2 }, 0, 1,
            new[] { deck, deck }, new MatchDrawRules(1, 2, Array.Empty<TestTurnPhase>()), new Random(1));
        match.DealOpeningHands(); Assert.AreEqual(1, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(Next()); Assert.AreEqual(2, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(0, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsTrue(match.ForPlayer(0).LastAction.Contains("Deck empty"));
        while (match.Phase != TestTurnPhase.Main) Assert.IsTrue(Next());
        Assert.IsFalse(match.TryCommand(0, match.MatchId, ++sequence, match.Revision, TestCommandKind.Summon, match.ForPlayer(0).OwnHand[0].Id, 2, out _));
        Assert.IsFalse(match.ValidateOverclock(0, match.ForPlayer(0).OwnHand[0].Id, 0, out _, out _));
    }
}
