using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class OpeningMatchTests
{
    [TestCase(0, 0, -1)] [TestCase(1, 1, -1)] [TestCase(2, 2, -1)]
    [TestCase(0, 1, 0)] [TestCase(1, 2, 0)] [TestCase(2, 0, 0)]
    [TestCase(1, 0, 1)] [TestCase(2, 1, 1)] [TestCase(0, 2, 1)]
    public void GestureOutcomes(int a, int b, int winner) => Assert.AreEqual(winner, OpeningDuel.Resolve(a, b));

    [Test] public void SealedChoiceCannotBeChangedOrReadByOpponentOrObserver()
    {
        var duel = new OpeningDuel(20, new Random(1));
        int epoch = duel.Epoch;
        Assert.IsTrue(duel.Choose(0, epoch, 2));
        Assert.AreEqual(2, duel.VisibleGesture(0, 0));
        Assert.AreEqual(-1, duel.VisibleGesture(0, 1));
        Assert.AreEqual(-1, duel.VisibleGesture(0, -1));
        Assert.IsFalse(duel.Choose(0, epoch, 0));
        Assert.IsFalse(duel.Choose(-1, epoch, 0));
        Assert.IsTrue(duel.Choose(1, epoch, 0));
        Assert.AreEqual(2, duel.VisibleGesture(0, -1));
        Assert.AreEqual(0, duel.Winner);
    }
    [Test] public void TieResetsRoundAndRejectsOldPackets()
    {
        var duel = new OpeningDuel(20, new Random(1));
        int epoch = duel.Epoch;
        duel.Choose(0, epoch, 1); duel.Choose(1, epoch, 1); duel.Advance(3);
        Assert.AreEqual(OpeningStage.Gesture, duel.Stage); Assert.AreEqual(2, duel.Round);
        Assert.IsFalse(duel.HasChosen(0)); Assert.IsFalse(duel.HasChosen(1));
        Assert.IsFalse(duel.Choose(0, epoch, 1));
        Assert.AreEqual(-1, duel.VisibleGesture(0, -1));
    }
    [Test] public void TimeoutFillsOnlyMissingGestureAndWinnerAloneChoosesOrder()
    {
        var duel = new OpeningDuel(20, new Random(1));
        duel.Choose(0, duel.Epoch, 0); duel.Advance(20);
        Assert.AreEqual(OpeningStage.Result, duel.Stage);
        Assert.AreEqual(0, duel.VisibleGesture(0, -1)); Assert.IsTrue(duel.HasChosen(1));
        var selected = new OpeningDuel(20, new Random(1));
        selected.Choose(0, selected.Epoch, 0); selected.Choose(1, selected.Epoch, 1); selected.Advance(3);
        Assert.IsFalse(selected.Choose(1, selected.Epoch, 0));
        Assert.IsTrue(selected.Choose(0, selected.Epoch, 1));
        Assert.AreEqual(1, selected.FirstPlayer); Assert.AreEqual(OpeningStage.Hand, selected.Stage);
    }
    [Test] public void OrderTimeoutDefaultsToWinnerGoingFirst()
    {
        var duel = new OpeningDuel(20, new Random(1));
        duel.Choose(0, duel.Epoch, 1); duel.Choose(1, duel.Epoch, 0); duel.Advance(3); duel.Advance(20);
        Assert.AreEqual(1, duel.FirstPlayer); Assert.AreEqual(OpeningStage.Hand, duel.Stage);
    }
    private static NetworkTestMatch Match(int time, bool decision = false, int opening = 5)
    {
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, contract: true) }
            .Concat(Enumerable.Range(0, 49).Select(i => new NetworkTestMatch.Card(Guid.Empty, "card" + i, 0, -1, zone: TestCardZone.Deck, decision: decision, time: time))).ToArray();
        return NetworkTestMatch.FromDecks(new BattleBoard(), new[] { 0, 1, 2 }, 0, 1, new[] { deck, deck },
            new MatchDrawRules(opening, 1, new[] { TestTurnPhase.Rebuild, TestTurnPhase.TimeReset, TestTurnPhase.Combat }), new Random(7));
    }
    [TestCase(4, false, false)] [TestCase(0, false, false)] [TestCase(5, false, true)] [TestCase(0, true, true)]
    public void RedrawEligibilityUsesPrintedUnitTime(int time, bool decision, bool allowed)
    {
        var match = Match(time, decision); match.DealOpeningHands(true);
        Assert.AreEqual(allowed, match.CanRedrawOpeningHand(0));
        Assert.AreEqual(allowed, match.DecideOpeningHand(0, true, out var shown));
        Assert.AreEqual(allowed ? 5 : 0, shown.Length);
    }
    [Test] public void RedrawIsOnceOnlyPubliclyRevealsOldHandAndPreservesDeckAndClocks()
    {
        var match = Match(5); match.SetFirstPlayer(1); match.DealOpeningHands(true);
        var oldHand = match.ForPlayer(0).OwnHand.Select(c => c.Id).ToArray();
        Assert.IsFalse(match.TimerRunning); Assert.IsFalse(match.AdvanceAutomaticPhases());
        Assert.IsFalse(match.TryCommand(1, match.MatchId, 1, match.Revision, TestCommandKind.NextPhase, Guid.Empty, -1, out _));
        Assert.IsTrue(match.DecideOpeningHand(0, true, out var shown));
        CollectionAssert.AreEquivalent(oldHand, shown.Select(c => c.Id));
        CollectionAssert.AreEquivalent(shown.Select(c => c.DefinitionId), match.DrainPresentations().Select(p => p.DefinitionId));
        Assert.IsFalse(match.DecideOpeningHand(0, true, out _)); Assert.IsFalse(match.CanRedrawOpeningHand(0));
        Assert.AreEqual(5, match.ForPlayer(0).OwnHand.Length); Assert.AreEqual(44, match.ForPlayer(0).DeckCounts[0]);
        Assert.AreEqual(0, match.ForPlayer(0).PublicPiles.Length);
        CollectionAssert.AreEqual(new[] { 4, 4 }, match.ForPlayer(0).CostPointers);
        CollectionAssert.AreEqual(new[] { 4, 4 }, match.ForPlayer(0).DamagePointers);
        Assert.AreEqual(0, match.ForObserver().OwnHand.Length);
        match.CompleteOpeningHands(); Assert.IsTrue(match.OpeningPending);
        match.DecideOpeningHand(1, false, out _); match.CompleteOpeningHands();
        Assert.IsFalse(match.OpeningPending); Assert.AreEqual(1, match.ActivePlayer);
        Assert.IsTrue(match.AdvanceAutomaticPhases()); Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        Assert.AreEqual(6, match.ForPlayer(1).OwnHand.Length);
    }
    [Test] public void DecliningIsFinalAndDoesNotRevealCards()
    {
        var match = Match(5); match.DealOpeningHands(true);
        Assert.IsTrue(match.DecideOpeningHand(0, false, out var shown));
        Assert.AreEqual(0, shown.Length); Assert.AreEqual(0, match.DrainPresentations().Length);
        Assert.IsFalse(match.CanRedrawOpeningHand(0)); Assert.IsFalse(match.DecideOpeningHand(0, true, out _));
    }
    [Test] public void OpeningDealRemainsIdempotentWhileWaitingForDecisions()
    {
        var match = Match(5); match.DealOpeningHands(true); match.DealOpeningHands();
        Assert.IsTrue(match.OpeningPending); Assert.AreEqual(5, match.ForPlayer(0).OwnHand.Length);
        Assert.Throws<InvalidOperationException>(() => match.SetFirstPlayer(1));
        Assert.IsFalse(match.DecideOpeningHand(-1, true, out _));
        var empty = Match(5, opening: 0); empty.DealOpeningHands(true); Assert.IsFalse(empty.CanRedrawOpeningHand(0));
    }
}
