using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class TurnTimerTests
{
    private NetworkTestMatch match;
    private long sequence;
    private void Create(int responseSeconds = 4, bool deal = true)
    {
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2); board.Connect(0, 3); board.SetPlayerNode(3, 1);
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, 1000, true) }
            .Concat(Enumerable.Range(0, 49).Select(i => new NetworkTestMatch.Card(Guid.Empty, "card" + i, 0, -1, 1000, false,
                TestCardZone.Deck, i % 2 == 0, i % 2 == 0 ? 0 : 1))).ToArray();
        match = NetworkTestMatch.FromDecks(board, new[] { 0, 1, 2, 3 }, 0, 1, new[] { deck, deck },
            new MatchDrawRules(10, 1, Array.Empty<TestTurnPhase>(), 10, 3, responseSeconds), new Random(5));
        sequence = 0; if (deal) match.DealOpeningHands();
    }
    private bool Act(TestCommandKind kind, Guid card = default, int node = -1, int? player = null) =>
        match.TryCommand(player ?? match.ActivePlayer, match.MatchId, ++sequence, match.Revision, kind, card, node, out _);
    private void Phase(TestTurnPhase target) { while (match.Phase < target) Assert.IsTrue(Act(TestCommandKind.NextPhase)); }
    private Guid Hand(int player, bool decision) => match.ForPlayer(player).OwnHand.First(c => c.IsDecision == decision).Id;
    private Guid Contract(int player = 0) => match.ForPlayer(player).Board.Single(c => c.Owner == player && c.IsContract).Id;
    [Test] public void OpeningAndFinishedMatchDoNotSpendTime()
    {
        Create(deal: false); Assert.IsFalse(match.AdvanceTime(100)); Assert.AreEqual(10, match.RemainingTurnSeconds);
        match.DealOpeningHands(); match.AdvanceTime(2); Assert.AreEqual(8, match.RemainingTurnSeconds);
        match.ResolvePlayerDamage(1, 8); Assert.IsFalse(match.AdvanceTime(100)); Assert.AreEqual(8, match.RemainingTurnSeconds);
    }
    [Test] public void OnlyAcceptedGameActionsRefundAndRefundIsCapped()
    {
        Create(); match.AdvanceTime(4); Phase(TestTurnPhase.Main); Assert.AreEqual(6, match.RemainingTurnSeconds);
        int revision = match.Revision;
        Assert.IsFalse(Act(TestCommandKind.Summon, Hand(0, false), 1));
        Assert.AreEqual(6, match.RemainingTurnSeconds); Assert.AreEqual(revision, match.Revision);
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(0, false), 2)); Assert.AreEqual(9, match.RemainingTurnSeconds);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(0, true))); Assert.AreEqual(10, match.RemainingTurnSeconds);
    }
    [Test] public void TimeoutResetsNextTurnAndRejectsOldTurnRequest()
    {
        Create(); var revision = match.Revision;
        Assert.IsTrue(match.AdvanceTime(10)); Assert.AreEqual(1, match.ActivePlayer);
        Assert.AreEqual(2, match.Turn); Assert.AreEqual(TestTurnPhase.TurnStart, match.Phase);
        Assert.AreEqual(10, match.RemainingTurnSeconds);
        Assert.IsFalse(match.TryCommand(0, match.MatchId, ++sequence, revision, TestCommandKind.NextPhase, Guid.Empty, -1, out _));
        Assert.AreEqual(2, match.Turn); Assert.AreEqual(10, match.RemainingTurnSeconds);
    }
    [Test] public void ManualTurnEndResetsAndLargeElapsedOnlyExpiresOneTurn()
    {
        Create(); match.AdvanceTime(7); Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(10, match.RemainingTurnSeconds);
        Assert.IsTrue(match.AdvanceTime(999)); Assert.AreEqual(3, match.Turn); Assert.AreEqual(10, match.RemainingTurnSeconds);
    }
    [Test] public void TimeoutCancelsPendingOccupationWithoutMovingAttacker()
    {
        Create(0); Phase(TestTurnPhase.Combat); var attacker = Contract();
        Assert.IsTrue(Act(TestCommandKind.Attack, attacker, 1)); Assert.AreEqual(1, match.ForPlayer(0).OccupationNode);
        Assert.IsTrue(match.AdvanceTime(10)); Assert.AreEqual(-1, match.ForPlayer(0).OccupationNode);
        Assert.AreEqual(0, match.ForPlayer(0).Board.Single(c => c.Id == attacker).NodeId);
    }
    [Test] public void OpponentResponsePausesTurnAndUsesIndependentBudget()
    {
        Create(); Phase(TestTurnPhase.Combat); match.AdvanceTime(5);
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.AreEqual(8, match.RemainingTurnSeconds); Assert.IsFalse(match.TimerRunning); Assert.AreEqual(1, match.ResponsePlayer);
        match.AdvanceTime(1); Assert.AreEqual(3, match.RemainingResponseSeconds); Assert.AreEqual(8, match.RemainingTurnSeconds);
        Assert.IsFalse(Act(TestCommandKind.PassResponse, player: 0));
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, Hand(1, false), player: 1));
        Assert.AreEqual(3, match.RemainingResponseSeconds);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(1, true), player: 1));
        Assert.AreEqual(0, match.ResponsePlayer); Assert.AreEqual(4, match.RemainingResponseSeconds);
        match.AdvanceTime(2); Assert.IsTrue(Act(TestCommandKind.PassResponse));
        Assert.AreEqual(1, match.ResponsePlayer); Assert.AreEqual(4, match.RemainingResponseSeconds);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(1, true), player: 1));
        Assert.AreEqual(2, match.RemainingResponseSeconds, "Priority switches must not refill the attacker's response budget.");
        Assert.AreEqual(8, match.RemainingTurnSeconds);
    }
    [Test] public void ResponseExpiryPassesRatherThanEndingOtherPlayersTurn()
    {
        Create(); Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(match.AdvanceTime(4)); Assert.AreEqual(0, match.ResponsePlayer); Assert.AreEqual(1, match.Turn);
        Assert.IsTrue(match.AdvanceTime(4)); Assert.AreEqual(-1, match.ResponsePlayer); Assert.AreEqual(1, match.Turn);
        Assert.IsTrue(match.TimerRunning); Assert.AreEqual(10, match.RemainingTurnSeconds);
        Assert.AreEqual(1, match.ForPlayer(0).PublicPiles.Count(c => c.IsContract));
        Assert.IsTrue(match.AdvanceTime(10)); Assert.AreEqual(2, match.Turn);
    }
    [Test] public void OwnTurnDecisionLimitAlsoAppliesInResponse()
    {
        Create(); Phase(TestTurnPhase.Main); Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(0, true)));
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(Act(TestCommandKind.PassResponse, player: 1));
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, Hand(0, true)));
        Assert.IsTrue(Act(TestCommandKind.PassResponse)); Assert.AreEqual(-1, match.ResponsePlayer);
    }
    [Test] public void PlayerDamageWaitsForBothResponses()
    {
        Create(); Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.AttackPlayer, Contract(), 3));
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[1]);
        Assert.IsTrue(Act(TestCommandKind.PassResponse, player: 1)); Assert.IsTrue(Act(TestCommandKind.PassResponse));
        Assert.AreEqual(5, match.ForPlayer(0).DamagePointers[1]);
    }
}
