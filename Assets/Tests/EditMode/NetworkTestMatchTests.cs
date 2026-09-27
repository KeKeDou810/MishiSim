using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class NetworkTestMatchTests
{
    private NetworkTestMatch match;
    private long[] sequences;
    [SetUp] public void Setup()
    {
        var board = new BattleBoard();
        board.Connect(0, 1); board.Connect(1, 2); board.Connect(0, 3); board.Connect(0, 2); board.Connect(1, 4);
        board.SetPlayerOrDefenseNode(3, 1); board.SetPlayerOrDefenseNode(5, 0);
        match = new NetworkTestMatch(board, new[] { 0, 1, 2, 3, 4, 5 }, 0, 2, "contract", "normal");
        sequences = new long[2];
    }
    private Guid Contract(int player) => match.ForPlayer(player).Board.Single(c => c.Owner == player && c.IsContract).Id;
    private Guid Hand(int player) => match.ForPlayer(player).OwnHand.First().Id;
    private bool Act(TestCommandKind kind, Guid card = default, int node = -1, int? player = null)
    {
        int p = player ?? match.ActivePlayer;
        return match.TryCommand(p, match.MatchId, ++sequences[p], match.Revision, kind, card, node, out _);
    }
    private void Phase(TestTurnPhase phase)
    { while (match.Phase < phase) Assert.IsTrue(Act(TestCommandKind.NextPhase)); Assert.AreEqual(phase, match.Phase); }
    private void PassTurn() { Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); }
    private NetworkTestMatch.Card Card(Guid id) => match.ForPlayer(0).Board.Concat(match.ForPlayer(0).PublicPiles).Single(c => c.Id == id);

    [Test] public void PhaseOrderAndEndTurnAreEnforced()
    {
        Assert.AreEqual(TestTurnPhase.TurnStart, match.Phase);
        Assert.IsFalse(Act(TestCommandKind.EndTurn));
        foreach (TestTurnPhase p in Enum.GetValues(typeof(TestTurnPhase)))
        { if (p == TestTurnPhase.TurnStart) continue; Assert.IsTrue(Act(TestCommandKind.NextPhase)); Assert.AreEqual(p, match.Phase); }
        Assert.IsFalse(Act(TestCommandKind.NextPhase));
        Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(1, match.ActivePlayer); Assert.AreEqual(TestTurnPhase.TurnStart, match.Phase);
    }
    [TestCase(TestTurnPhase.TurnStart)] [TestCase(TestTurnPhase.Draw)] [TestCase(TestTurnPhase.Rebuild)]
    [TestCase(TestTurnPhase.TimeReset)] [TestCase(TestTurnPhase.Combat)] [TestCase(TestTurnPhase.End)]
    public void CannotSummonOrMoveOutsideMain(TestTurnPhase phase)
    {
        Phase(phase); int revision = match.Revision;
        Assert.IsFalse(Act(TestCommandKind.Summon, Hand(0), 4));
        Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(0), 1));
        Assert.AreEqual(revision, match.Revision);
    }
    [Test] public void LegalMoveUpdatesBothRecipientsWithSameIdentity()
    {
        Phase(TestTurnPhase.Main); var id = Contract(0); int revision = match.Revision;
        Assert.IsTrue(Act(TestCommandKind.MoveContract, id, 1)); Assert.AreEqual(revision + 1, match.Revision);
        foreach (int player in new[] { 0, 1 }) Assert.AreEqual(1, match.ForPlayer(player).Board.Single(c => c.Id == id).NodeId);
    }
    [Test] public void OpponentCannotActOrAdvancePhase()
    { Assert.IsFalse(Act(TestCommandKind.NextPhase, player: 1)); Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.Summon, Hand(1), 4, 1)); }
    [Test] public void CannotControlOtherPlayersCard()
    { Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(1), 1)); Assert.IsFalse(Act(TestCommandKind.Summon, Hand(1), 4)); }
    [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(999)]
    public void RejectOccupiedProtectedDisconnectedAndUnknownMoves(int target)
    { Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(0), target)); Assert.AreEqual(0, Card(Contract(0)).NodeId); }
    [Test] public void RejectSecondMoveInSameTurn()
    { Phase(TestTurnPhase.Main); Assert.IsTrue(Act(TestCommandKind.MoveContract, Contract(0), 1)); Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(0), 0)); }
    [Test] public void RejectDuplicateSequence()
    {
        Phase(TestTurnPhase.Main); long seq = ++sequences[0];
        Assert.IsFalse(match.TryCommand(0, match.MatchId, seq, match.Revision, TestCommandKind.Summon, Hand(0), 999, out _));
        Assert.IsFalse(match.TryCommand(0, match.MatchId, seq, match.Revision, TestCommandKind.Summon, Hand(0), 4, out _));
    }
    [Test] public void RejectStaleRevisionOldMatchAndUnknownCommand()
    {
        Assert.IsFalse(match.TryCommand(0, match.MatchId, 1, 42, TestCommandKind.NextPhase, Guid.Empty, 0, out _));
        Assert.IsFalse(match.TryCommand(0, Guid.NewGuid(), 2, 0, TestCommandKind.NextPhase, Guid.Empty, 0, out _));
        Assert.IsFalse(match.TryCommand(0, match.MatchId, 3, 0, (TestCommandKind)99, Guid.Empty, 0, out _));
    }
    [Test] public void SummonNeedsNoAdjacencyAndNoCostAndKeepsIdentity()
    {
        Phase(TestTurnPhase.Main); var first = Hand(0);
        Assert.IsTrue(Act(TestCommandKind.Summon, first, 4)); // deliberately unconnected node
        Assert.AreEqual(first, match.ForPlayer(1).Board.Single(c => c.NodeId == 4).Id);
        Assert.IsFalse(Card(first).Tapped); Assert.AreEqual(3, match.ForPlayer(1).OpponentHandCount);
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(0), 5)); // own protected node
        Assert.AreEqual(2, match.ForPlayer(0).OwnHand.Length);
    }
    [TestCase(0)] [TestCase(3)] [TestCase(999)]
    public void IllegalSummonDoesNotRemoveHandCard(int node)
    { Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.Summon, Hand(0), node)); Assert.AreEqual(4, match.ForPlayer(0).OwnHand.Length); }
    [Test] public void NormalUnitsCannotUseContractMovement()
    { Phase(TestTurnPhase.Main); var id = Hand(0); Assert.IsTrue(Act(TestCommandKind.Summon, id, 1)); Assert.IsFalse(Act(TestCommandKind.MoveContract, id, 4)); }
    [Test] public void AttacksOnlyInCombat()
    { Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.Attack, Contract(0), 2)); Assert.IsFalse(Card(Contract(0)).Tapped); }
    [Test] public void SummonedUnitCanAttackImmediatelyAndTaps()
    {
        Phase(TestTurnPhase.Main); var id = Hand(0); Assert.IsTrue(Act(TestCommandKind.Summon, id, 1));
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, id, 2));
        Assert.IsTrue(Card(id).Tapped); Assert.AreEqual(TestCardZone.Contract, match.ForPlayer(0).PublicPiles.Single().Zone);
        Assert.IsTrue(Act(TestCommandKind.Stay)); Assert.IsFalse(Act(TestCommandKind.Attack, id, 2));
    }
    [Test] public void OccupationMustBeResolvedBeforePhaseOrNextAction()
    {
        Phase(TestTurnPhase.Combat); var id = Contract(0);
        Assert.IsTrue(Act(TestCommandKind.Attack, id, 2));
        Assert.IsFalse(Act(TestCommandKind.NextPhase)); Assert.IsFalse(Act(TestCommandKind.EndTurn));
        Assert.IsFalse(Act(TestCommandKind.Occupy, player: 1));
        Assert.IsTrue(Act(TestCommandKind.Occupy)); Assert.AreEqual(2, Card(id).NodeId); Assert.IsTrue(Card(id).Tapped);
    }
    [Test] public void TapPersistsThroughOpponentTurnAndResetsOnlyOnOwnRebuild()
    {
        Phase(TestTurnPhase.Combat); var id = Contract(0); Assert.IsTrue(Act(TestCommandKind.Attack, id, 2)); Assert.IsTrue(Act(TestCommandKind.Stay));
        PassTurn(); Phase(TestTurnPhase.Rebuild); Assert.IsTrue(Card(id).Tapped);
        PassTurn(); Assert.IsTrue(Card(id).Tapped); Phase(TestTurnPhase.Draw); Assert.IsTrue(Card(id).Tapped);
        Phase(TestTurnPhase.Rebuild); Assert.IsFalse(Card(id).Tapped);
    }
    [Test] public void InvalidAttackDoesNotTapAttacker()
    {
        Phase(TestTurnPhase.Main); Assert.IsTrue(Act(TestCommandKind.Summon, Hand(0), 1)); Phase(TestTurnPhase.Combat);
        Assert.IsFalse(Act(TestCommandKind.Attack, Contract(0), 1)); Assert.IsFalse(Act(TestCommandKind.Attack, Contract(0), 4)); Assert.IsFalse(Card(Contract(0)).Tapped);
    }
    [Test] public void ContractDestroysOrdinaryTargetButCannotOccupyEnemyProtectedNode()
    {
        Phase(TestTurnPhase.Main); var attacker = Hand(0); Assert.IsTrue(Act(TestCommandKind.Summon, attacker, 1));
        PassTurn(); Phase(TestTurnPhase.Main); Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(1), 1)); // occupied
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(1), 3));
        // P0 contract attacks ordinary at node 3 first to cover contract auto-destruction.
        PassTurn(); Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(0), 3));
        Assert.AreEqual(TestCardZone.Discard, match.ForPlayer(0).PublicPiles.Single().Zone);
        Assert.AreEqual(-1, match.ForPlayer(0).OccupationNode); // enemy protected node
    }
    [Test] public void EqualOrdinaryPowerWinsAndTappedAttackerCannotAttackAgain()
    {
        Phase(TestTurnPhase.Main); var attacker = Hand(0); Assert.IsTrue(Act(TestCommandKind.Summon, attacker, 1));
        PassTurn(); Phase(TestTurnPhase.Main); var target = Hand(1); Assert.IsTrue(Act(TestCommandKind.Summon, target, 4));
        PassTurn(); Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, attacker, 4));
        Assert.AreEqual(TestCardZone.Discard, Card(target).Zone); Assert.IsTrue(Card(attacker).Tapped);
        Assert.IsTrue(Act(TestCommandKind.Stay));
        Assert.IsFalse(Act(TestCommandKind.Attack, attacker, 2)); // valid adjacent enemy, but attacker is tapped
    }
    [Test] public void WeakerNormalAttackerTapsButNeitherUnitLeavesBoard()
    {
        var board = new BattleBoard(); board.Connect(1, 2);
        match = new NetworkTestMatch(board, new[] { 0, 1, 2 }, 0, 2, "contract", "normal", 5000, 4000);
        Phase(TestTurnPhase.Main); var attacker = Hand(0); var target = Contract(1);
        Assert.IsTrue(Act(TestCommandKind.Summon, attacker, 1)); Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.Attack, attacker, 2)); Assert.IsTrue(Card(attacker).Tapped);
        Assert.AreEqual(TestCardZone.Board, Card(target).Zone); Assert.AreEqual(TestCardZone.Board, Card(attacker).Zone);
        Assert.AreEqual(-1, match.ForPlayer(0).OccupationNode);
    }
    [Test] public void SummonedCardCannotBeSummonedAgain()
    {
        Phase(TestTurnPhase.Main); var id = Hand(0); Assert.IsTrue(Act(TestCommandKind.Summon, id, 4));
        Assert.IsFalse(Act(TestCommandKind.Summon, id, 5)); Assert.AreEqual(4, Card(id).NodeId);
    }
    [Test] public void OverclockValidationIsTypedAndReservedCommandIsNonMutating()
    {
        Phase(TestTurnPhase.Main); var hand = Hand(0); var target = Contract(0); int revision = match.Revision;
        Assert.IsTrue(match.ValidateOverclock(0, hand, 0, out var request, out _));
        Assert.AreEqual(hand, request.SourceCardId); Assert.AreEqual(target, request.TargetCardId); Assert.AreEqual(match.MatchId, request.MatchId);
        Assert.IsFalse(Act(TestCommandKind.Overclock, hand, 0)); Assert.AreEqual(revision, match.Revision);
        Assert.AreEqual(4, match.ForPlayer(0).OwnHand.Length); Assert.AreEqual(2, match.ForPlayer(0).Board.Length);
        Assert.IsFalse(match.ValidateOverclock(0, hand, 2, out _, out _));
    }
    [Test] public void PrivateViewsNeverContainOpponentHandIdentity()
    {
        var a = match.ForPlayer(0); var b = match.ForPlayer(1);
        Assert.AreEqual(4, a.OpponentHandCount); Assert.IsTrue(a.OwnHand.All(c => c.Owner == 0));
        Assert.IsFalse(a.OwnHand.Concat(a.Board).Concat(a.PublicPiles).Any(c => b.OwnHand.Any(other => other.Id == c.Id)));
    }
    [Test] public void SnapshotMutationCannotChangeAuthoritativeState()
    { var view = match.ForPlayer(0); view.Board[0] = null; view.DeckCounts[0] = 99; Assert.NotNull(match.ForPlayer(0).Board[0]); Assert.AreEqual(0, match.ForPlayer(0).DeckCounts[0]); }
    [Test] public void NewMatchDoesNotReuseInstanceIdentity()
    { var first = match.ForPlayer(0); Setup(); Assert.AreNotEqual(first.MatchId, match.MatchId); Assert.AreNotEqual(first.Board[0].Id, match.ForPlayer(0).Board[0].Id); }
}
