using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class ClockBattleTests
{
    private NetworkTestMatch match;
    private long sequence;
    private void Create(bool decisions = false, int size = 50, int opening = 5, int time = 2, TestTurnPhase[] skipped = null)
    {
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 3); board.Connect(2, 3);
        board.SetPlayerOrDefenseNode(1, 1); board.SetPlayerNode(3, 1); board.SetPlayerNode(4, 0);
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, 1000, true) }
            .Concat(Enumerable.Range(0, size - 1).Select(i => new NetworkTestMatch.Card(Guid.Empty, "unit", 0, -1, 4000, false, TestCardZone.Deck, decisions, time))).ToArray();
        match = NetworkTestMatch.FromDecks(board, new[] { 0, 1, 2, 3, 4, 5, 6 }, 0, 1,
            new[] { deck, deck }, new MatchDrawRules(opening, 1, skipped ?? Array.Empty<TestTurnPhase>()), new Random(2));
        match.DealOpeningHands(); sequence = 0;
    }
    private bool Act(TestCommandKind kind, Guid card = default, int node = -1) =>
        match.TryCommand(match.ActivePlayer, match.MatchId, ++sequence, match.Revision, kind, card, node, out _);
    private void Phase(TestTurnPhase target) { while (match.Phase < target) Assert.IsTrue(Act(TestCommandKind.NextPhase)); }
    [Test] public void AutomaticPhasesStopAtMainAndCombatAndRunDrawAndTimeResetOnce()
    {
        Create();
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        Assert.AreEqual(6, match.ForPlayer(0).OwnHand.Length);
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        Assert.IsTrue(Act(TestCommandKind.NextPhase));
        Assert.AreEqual(TestTurnPhase.Combat, match.Phase);
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        match.ResolvePlayerDamage(1, 1);
        Assert.IsTrue(Act(TestCommandKind.NextPhase));
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(1, match.ActivePlayer);
        Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        Assert.AreEqual(6, match.ForPlayer(1).OwnHand.Length);
        Assert.AreEqual(5, match.ForPlayer(1).CostPointers[1]);
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        Assert.AreEqual(6, match.ForPlayer(1).OwnHand.Length);
    }
    [Test] public void AutomaticPhasesRespectFirstTurnSkipsAndGameOver()
    {
        Create(skipped: new[] { TestTurnPhase.Rebuild, TestTurnPhase.TimeReset, TestTurnPhase.Combat });
        match.ResolvePlayerDamage(0, 1);
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(Act(TestCommandKind.NextPhase));
        Assert.AreEqual(TestTurnPhase.End, match.Phase);
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(1, match.ActivePlayer);
        Assert.AreEqual(TestTurnPhase.Main, match.Phase);
        match.ResolvePlayerDamage(0, 7);
        int revision = match.Revision;
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        Assert.AreEqual(revision, match.Revision);
    }
    private Guid Hand() => match.ForPlayer(match.ActivePlayer).OwnHand.First().Id;
    private Guid Contract() => match.ForPlayer(match.ActivePlayer).Board.Single(c => c.Owner == match.ActivePlayer && c.IsContract).Id;
    private void EndTurn() { Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); }
    [Test] public void SummonsPayOnlyAfterLegalityChecksAndInsufficientPaymentIsRejected()
    {
        Create(); Phase(TestTurnPhase.Main);
        Assert.IsFalse(Act(TestCommandKind.Summon, Hand(), 1)); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2)); Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 5)); Assert.AreEqual(12, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(-1, match.Winner);
        int revision = match.Revision;
        var hand = Hand(); int handCount = match.ForPlayer(0).OwnHand.Length;
        Assert.IsFalse(Act(TestCommandKind.Summon, hand, 6)); Assert.AreEqual(-1, match.Winner);
        Assert.AreEqual(revision, match.Revision);
        Assert.AreEqual(12, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(handCount, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == hand));
        Assert.IsTrue(Act(TestCommandKind.NextPhase));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.NodeId == 6));
    }
    [Test] public void ContractDefeatFlipsOwnerAndDrawsBeforePlayerAttack()
    {
        Create(); Phase(TestTurnPhase.Main); Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Phase(TestTurnPhase.Combat);
        Assert.IsFalse(Act(TestCommandKind.AttackPlayer, Contract(), 3)); // defended
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        var view = match.ForPlayer(1);
        Assert.IsTrue(view.PlayerFlipped[1]); Assert.IsFalse(view.PlayerFlipped[0]);
        Assert.AreEqual(6, view.OwnHand.Length); Assert.AreEqual(43, view.DeckCounts[1]);
        Assert.AreEqual(TestCardZone.Contract, view.PublicPiles.Single().Zone);
        var unit = view.Board.Single(c => c.NodeId == 2);
        Assert.IsTrue(Act(TestCommandKind.AttackPlayer, unit.Id, 3));
        view = match.ForPlayer(1);
        Assert.AreEqual(5, view.DamagePointers[1]); Assert.AreEqual(5, view.CostPointers[1]);
        Assert.IsTrue(view.Board.Single(c => c.Id == unit.Id).Tapped);
        Assert.IsFalse(Act(TestCommandKind.AttackPlayer, unit.Id, 3));
    }
    [Test] public void DecisionsPayAndOwnTurnLimitResetsNextTurn()
    {
        Create(true); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand())); Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]);
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, Hand())); Assert.AreEqual(1, match.ForPlayer(0).PublicPiles.Length);
        EndTurn(); EndTurn(); Phase(TestTurnPhase.TimeReset);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand()));
    }
    [Test] public void InsufficientDecisionPaymentPreservesCardAndUsageAllowance()
    {
        Create(true, time: 5); Phase(TestTurnPhase.Main);
        int revision = match.Revision; var hand = Hand();
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, hand));
        Assert.AreEqual(revision, match.Revision); Assert.AreEqual(-1, match.Winner);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(0, match.ForPlayer(0).PublicPiles.Length);
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == hand));
        match.ResolvePlayerDamage(0, 1, moveCost: true); // A later effect supplies the missing time.
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, hand));
        Assert.AreEqual(12, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void EmptyDeckImmediatelyRecyclesDiscardAndBothPointersMove()
    {
        Create(true, 5, 2); Phase(TestTurnPhase.Main); // 1 card left
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand())); // one discard
        EndTurn(); EndTurn(); Phase(TestTurnPhase.Draw);
        var view = match.ForPlayer(0);
        Assert.AreEqual(1, view.DeckCounts[0]); Assert.AreEqual(0, view.PublicPiles.Length);
        Assert.AreEqual(6, view.DamagePointers[0]); Assert.AreEqual(4, view.CostPointers[0]);
    }
    [Test] public void LethalDamagePublishesWinnerToBothViewsAndFreezesActions()
    {
        Create(); match.ResolvePlayerDamage(1, 8);
        Assert.AreEqual(0, match.ForPlayer(0).Winner); Assert.AreEqual(0, match.ForPlayer(1).Winner);
        Assert.AreEqual(12, match.ForPlayer(0).DamagePointers[1]); Assert.IsFalse(Act(TestCommandKind.NextPhase));
    }
    [Test] public void DamageAddsCostAndOnlyOwnersTimeResetRestoresItToDamagePointer()
    {
        Create(time: 3); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.AreEqual(1, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[0]);
        match.ResolvePlayerDamage(0, 1, moveCost: true);
        foreach (int recipient in new[] { 0, 1 })
        {
            Assert.AreEqual(2, match.ForPlayer(recipient).CostPointers[0]);
            Assert.AreEqual(5, match.ForPlayer(recipient).DamagePointers[0]);
        }
        EndTurn(); Phase(TestTurnPhase.TimeReset);
        Assert.AreEqual(1, match.ActivePlayer);
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0], "Opponent's time reset must not restore this player's cost.");
        EndTurn(); Phase(TestTurnPhase.Rebuild);
        Assert.AreEqual(0, match.ActivePlayer);
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0], "Ordinary rebuild only untaps cards.");
        Phase(TestTurnPhase.TimeReset);
        foreach (int recipient in new[] { 0, 1 })
        {
            Assert.AreEqual(5, match.ForPlayer(recipient).CostPointers[0]);
            Assert.AreEqual(5, match.ForPlayer(recipient).DamagePointers[0]);
        }
    }
}
