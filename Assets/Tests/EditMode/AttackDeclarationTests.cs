using Mishi.Battle;
using NUnit.Framework;

public sealed class AttackDeclarationTests
{
    private static BattleState Create(BattlePhase phase = BattlePhase.Combat, bool exhausted = false, bool adjacent = true, int targetPlayer = 1, UnitZone targetZone = UnitZone.Board)
    {
        var board = new BattleBoard();
        if (adjacent) board.Connect(10, 20);
        return new BattleState(board, 0, phase, new[] {
            new BattleUnit(1, "same-definition", 0, 10, 4000, isExhausted: exhausted),
            new BattleUnit(2, "same-definition", targetPlayer, 20, 1000, zone: targetZone)
        });
    }
    private static void Reject(BattleState state, AttackError expected, int player = 0, int attacker = 1, int target = 2)
    {
        bool before = state.GetUnit(1).IsExhausted;
        Assert.AreEqual(expected, state.DeclareAttack(player, attacker, target));
        Assert.AreEqual(before, state.GetUnit(1).IsExhausted);
        Assert.IsFalse(state.GetUnit(2).IsExhausted);
        Assert.IsNull(state.PendingAttack);
    }
    [Test] public void ValidAttackExhaustsOnlyAttackerAndWaitsForDefender()
    {
        BattleState state = Create();
        Assert.AreEqual(AttackError.None, state.DeclareAttack(0, 1, 2));
        Assert.IsTrue(state.GetUnit(1).IsExhausted);
        Assert.IsFalse(state.GetUnit(2).IsExhausted);
        Assert.AreEqual(1, state.PendingAttack.RespondingPlayerId);
        Assert.AreEqual(UnitZone.Board, state.GetUnit(2).Zone);
        Assert.AreEqual(1, state.PendingAttack.AttackerId);
        Assert.AreEqual(2, state.PendingAttack.TargetId);
    }
    [Test] public void RejectsWrongPhase() => Reject(Create(BattlePhase.Main), AttackError.WrongPhase);
    [Test] public void RejectsWrongPlayer() => Reject(Create(), AttackError.WrongPlayer, player: 1);
    [Test] public void RejectsOpponentUnit() => Reject(Create(), AttackError.NotControlled, attacker: 2, target: 1);
    [Test] public void RejectsFriendlyTarget() => Reject(Create(targetPlayer: 0), AttackError.FriendlyTarget);
    [Test] public void RejectsExhaustedAttacker() => Reject(Create(exhausted: true), AttackError.Exhausted);
    [Test] public void RejectsUnconnectedNodes() => Reject(Create(adjacent: false), AttackError.OutOfRange);
    [Test] public void RejectsMissingTarget() => Reject(Create(), AttackError.MissingUnit, target: 999);
    [Test] public void RejectsTargetOutsideBoard() => Reject(Create(targetZone: UnitZone.Discard), AttackError.NotOnBoard);
    [Test] public void PreventsAnotherAttackDuringResponseWindow()
    {
        var state = Create(); state.DeclareAttack(0, 1, 2);
        PendingAttack pending = state.PendingAttack;
        Assert.AreEqual(AttackError.AttackAlreadyPending, state.DeclareAttack(0, 1, 2));
        Assert.AreSame(pending, state.PendingAttack);
    }
    [Test] public void MatchesDoNotShareInstanceState()
    {
        var board = new BattleBoard(); board.Connect(10, 20);
        var units = new[] { new BattleUnit(1, "a", 0, 10, 1), new BattleUnit(2, "b", 1, 20, 1) };
        var a = new BattleState(board, 0, BattlePhase.Combat, units);
        var b = new BattleState(board, 0, BattlePhase.Combat, units);
        a.DeclareAttack(0, 1, 2);
        Assert.IsFalse(units[0].IsExhausted);
        Assert.IsFalse(b.GetUnit(1).IsExhausted);
    }
}
