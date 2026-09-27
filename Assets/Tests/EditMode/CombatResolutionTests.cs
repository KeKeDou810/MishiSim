using Mishi.Battle;
using NUnit.Framework;

public sealed class CombatResolutionTests
{
    private static BattleState Create(int attackerPower, int targetPower, bool attackerContract = false, bool targetContract = false, bool protectedNode = false)
    {
        var board = new BattleBoard(); board.Connect(10, 20);
        if (protectedNode) board.SetPlayerOrDefenseNode(20, 1);
        var state = new BattleState(board, 0, BattlePhase.Combat, new[] {
            new BattleUnit(1, "a", 0, 10, attackerPower, attackerContract),
            new BattleUnit(2, "b", 1, 20, targetPower, targetContract)
        });
        state.DeclareAttack(0, 1, 2);
        return state;
    }
    private static void PassBoth(BattleState state)
    {
        Assert.IsTrue(state.PassResponse(1));
        Assert.IsTrue(state.PassResponse(0));
    }
    [Test] public void CannotResolveBeforeDefenderAndAttackerPass()
    {
        var s = Create(4000, 1000);
        Assert.IsFalse(s.PassResponse(0));
        Assert.IsFalse(s.ResolveWithoutCardEffects());
        s.PassResponse(1);
        Assert.IsFalse(s.ResolveWithoutCardEffects());
        s.PassResponse(0);
        Assert.IsTrue(s.ResolveWithoutCardEffects());
    }
    [Test] public void EqualPowerDestroysDefender()
    {
        var s = Create(1000, 1000); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.AreEqual(UnitZone.Discard, s.GetUnit(2).Zone);
        Assert.IsTrue(s.AwaitingOccupation);
    }
    [Test] public void LowerPowerLeavesBothOnBoard()
    {
        var s = Create(1000, 4000); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.AreEqual(UnitZone.Board, s.GetUnit(1).Zone);
        Assert.AreEqual(UnitZone.Board, s.GetUnit(2).Zone);
        Assert.IsTrue(s.GetUnit(1).IsExhausted);
        Assert.IsNull(s.PendingAttack);
    }
    [Test] public void AttackingContractIgnoresPower()
    {
        var s = Create(0, 99999, attackerContract: true); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.AreEqual(UnitZone.Discard, s.GetUnit(2).Zone);
    }
    [Test] public void OccupationRequiresAttackerChoice()
    {
        var s = Create(4000, 1000); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.AreEqual(10, s.GetUnit(1).NodeId);
        Assert.IsFalse(s.ChooseOccupation(1, true));
        Assert.IsTrue(s.ChooseOccupation(0, true));
        Assert.AreEqual(20, s.GetUnit(1).NodeId);
        Assert.IsNull(s.PendingAttack);
    }
    [Test] public void CanDeclineOccupation()
    {
        var s = Create(4000, 1000); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.IsTrue(s.ChooseOccupation(0, false));
        Assert.AreEqual(10, s.GetUnit(1).NodeId);
    }
    [Test] public void CannotOccupyOpposingDefenseOrPlayerNode()
    {
        var s = Create(4000, 1000, protectedNode: true); PassBoth(s); s.ResolveWithoutCardEffects();
        Assert.IsFalse(s.ChooseOccupation(0, true));
        Assert.AreEqual(10, s.GetUnit(1).NodeId);
    }
    [Test] public void ContractDestructionRequestsDrawAndLevelZeroFlip()
    {
        var s = Create(4000, 1000, targetContract: true); PassBoth(s);
        s.ResolveWithoutCardEffects();
        Assert.AreEqual(UnitZone.Contract, s.GetUnit(2).Zone);
        Assert.AreEqual(1, s.PendingContractDrawPlayerId);
        Assert.IsTrue(s.PendingLevelZeroPlayerFlip);
    }
}
