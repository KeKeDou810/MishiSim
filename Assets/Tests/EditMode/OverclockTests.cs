using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class OverclockTests
{
    private NetworkTestMatch match;
    private long sequence;
    [SetUp] public void Setup()
    {
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2); board.Connect(1, 2);
        board.SetPlayerOrDefenseNode(3, 1);
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, 6000, true) }
            .Concat(Enumerable.Range(0, 49).Select(i => new NetworkTestMatch.Card(Guid.Empty, "unit-" + (i % 6 + 1), 0, -1, 1000, false, TestCardZone.Deck, false, i % 6 + 1))).ToArray();
        match = NetworkTestMatch.FromDecks(board, new[] { 0, 1, 2, 3, 4 }, 0, 1,
            new[] { deck, deck }, new MatchDrawRules(40, 1, Array.Empty<TestTurnPhase>()), new Random(8));
        match.DealOpeningHands(); sequence = 0; Phase(TestTurnPhase.Main);
    }
    private bool Act(TestCommandKind kind, Guid card = default, int node = -1) =>
        match.TryCommand(match.ActivePlayer, match.MatchId, ++sequence, match.Revision, kind, card, node, out _);
    private void Phase(TestTurnPhase target) { while (match.Phase < target) Assert.IsTrue(Act(TestCommandKind.NextPhase)); }
    private Guid Hand(int time) => match.ForPlayer(match.ActivePlayer).OwnHand.First(c => c.Time == time).Id;
    private NetworkTestMatch.Card Top(int node) => match.ForPlayer(0).Board.Single(c => c.NodeId == node && !c.Covered);
    [Test] public void OverclockPaysPositiveDifferenceAndSupportsRepeatedStacking()
    {
        var source = Hand(1); var original = Top(0).Id;
        Assert.IsTrue(match.ValidateOverclock(0, source, 0, out var request, out _));
        Assert.AreEqual(original, request.TargetCardId); Assert.AreEqual(source, request.SourceCardId);
        Assert.IsTrue(Act(TestCommandKind.Overclock, source, 0));
        Assert.AreEqual(3, match.ForPlayer(0).CostPointers[0]); Assert.AreEqual(source, Top(0).Id);
        Assert.IsFalse(Top(0).Tapped);
        Assert.AreEqual(1, Top(0).StackOrder);
        var next = Hand(4); Assert.IsTrue(Act(TestCommandKind.Overclock, next, 0));
        Assert.AreEqual(12, match.ForPlayer(0).CostPointers[0]); Assert.AreEqual(2, Top(0).StackOrder);
        foreach (int player in new[] { 0, 1 })
        {
            var pile = match.ForPlayer(player).Board.Where(c => c.NodeId == 0).ToArray();
            Assert.AreEqual(3, pile.Length); Assert.AreEqual(2, pile.Count(c => c.Covered));
            Assert.AreEqual(next, pile.Single(c => !c.Covered).Id);
            Assert.IsTrue(pile.Where(c => c.Covered).All(c => c.Tapped));
            Assert.IsFalse(pile.Single(c => !c.Covered).Tapped);
        }
    }
    [Test] public void EqualLowerInsufficientEnemyAndEmptyTargetsAreRejected()
    {
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(1), 1));
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(1), 2));
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(1), 3));
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(2), 0));
        int revision = match.Revision, count = match.ForPlayer(0).OwnHand.Length;
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(2), 0));
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(1), 0));
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(5), 0));
        Assert.AreEqual(revision, match.Revision); Assert.AreEqual(count, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void CoveredContractCannotMoveOrAttackAndRemainsTapped()
    {
        Guid contract = Top(0).Id; Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(1), 0));
        Assert.IsFalse(Act(TestCommandKind.MoveContract, contract, 2));
        Phase(TestTurnPhase.Combat);
        Assert.IsFalse(Act(TestCommandKind.Attack, contract, 1));
        Assert.IsTrue(Act(TestCommandKind.Attack, Top(0).Id, 1)); // weaker than opposing contract
        Assert.IsTrue(Top(0).Tapped);
        Assert.IsTrue(match.ForPlayer(0).Board.Single(c => c.Id == contract).Tapped);
        Assert.IsFalse(Act(TestCommandKind.Overclock, Hand(2), 0));
    }
    [Test] public void DestroyingStackMovesAllCardsAndResolvesBuriedContractOnce()
    {
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(1), 0));
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(2), 0));
        int handCount = match.ForPlayer(0).OwnHand.Length;
        int deckCount = match.ForPlayer(0).DeckCounts[0];
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.Attack, Top(1).Id, 0));
        var view = match.ForPlayer(0);
        Assert.IsFalse(view.Board.Any(c => c.NodeId == 0));
        Assert.AreEqual(3, view.PublicPiles.Length);
        Assert.AreEqual(2, view.PublicPiles.Count(c => c.Zone == TestCardZone.Discard));
        Assert.AreEqual(1, view.PublicPiles.Count(c => c.Zone == TestCardZone.Contract));
        Assert.IsTrue(view.PublicPiles.All(c => !c.Covered && c.StackOrder == 0));
        Assert.IsTrue(view.PlayerFlipped[0]); Assert.AreEqual(handCount + 1, view.OwnHand.Length);
        Assert.AreEqual(deckCount - 1, view.DeckCounts[0]);
        Assert.AreEqual(0, view.OccupationNode);
        Assert.IsTrue(Act(TestCommandKind.Occupy)); Assert.AreEqual(1, Top(0).Owner);
    }
    [Test] public void OccupationMovesTheWholeAttackingStack()
    {
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(1), 0));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.Main); Assert.IsTrue(Act(TestCommandKind.Summon, Hand(2), 2));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Top(0).Id, 2));
        Assert.AreEqual(2, match.ForPlayer(0).OccupationNode);
        Assert.IsTrue(Act(TestCommandKind.Occupy));
        var view = match.ForPlayer(0);
        Assert.AreEqual(2, view.Board.Count(c => c.NodeId == 2));
        Assert.IsFalse(view.Board.Any(c => c.NodeId == 0));
        Assert.IsTrue(Top(2).Tapped);
    }
}
