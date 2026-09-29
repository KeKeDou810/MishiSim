using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    private void AttachmentSetup()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Definitions["normal"].HasActivate = true;
        provider.Definitions["player"] = new CardEffectRules { Type = "玩家卡", Name = "Player", HasActivate = true, ActivateZone = TestCardZone.Player };
        var board = new BattleBoard(); board.Connect(0, 2); board.Connect(2, 1); board.Connect(1, 3); board.Connect(2, 3); board.Connect(4, 3); board.SetPlayerNode(3, 0); board.SetPlayerNode(4, 1);
        match = new NetworkTestMatch(board, new[] { 0, 1, 2, 3, 4 }, 0, 1, "contract", "normal");
        match.AttachEffects(provider, ClockKind.Black, ClockKind.Black);
        foreach (int owner in new[] { 0, 1 }) match.AddPlayerCards(owner, owner + 3,
            new NetworkTestMatch.Card(Guid.NewGuid(), "player", owner, owner + 3), new NetworkTestMatch.Card(Guid.NewGuid(), "player", owner, owner + 3));
        sequence = 0; Phase(TestTurnPhase.Main);
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { EffectId = "AttachPlayers" } } : Array.Empty<EffectInstruction>();
    }
    [Test] public void PlayerPairFollowsCarrierWithoutOccupyingUnitSlotsOrFlipping()
    {
        AttachmentSetup(); var carrier = Contract();
        var face = match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.AttachedTo == carrier && c.NodeId == 0));
        Assert.AreEqual(1, match.ForPlayer(0).Board.Count(c => c.Owner == 0));
        Assert.IsTrue(Act(TestCommandKind.MoveContract, carrier, 2));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 2));
        Assert.AreEqual(face, match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id);
        Assert.IsTrue(match.CanActivateCard(face));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 1).All(c => c.NodeId == 4 && c.AttachedTo == Guid.Empty));
    }
    [Test] public void SuppressionReturnsPlayersWithoutFlipOrAutomaticReattachment()
    {
        AttachmentSetup(); var carrier = Contract(); var face = match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier));
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "SuppressEffects" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
        Assert.AreEqual(face, match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id);
        Assert.IsTrue(match.ForPlayer(1).Board.Any(c => c.Id == carrier));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
    }
    [Test] public void DestroyedCarrierReturnsPairUnderExistingHomeUnitAndReentryDoesNotReattach()
    {
        AttachmentSetup(); var guard = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, guard, 3));
        var carrier = Contract(); var oldFace = match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Destroy } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
        Assert.AreNotEqual(oldFace, match.PlayerCards.Single(c => c.Owner == 0 && !c.Covered).Id);
        Assert.IsTrue(match.ForPlayer(1).Board.Any(c => c.Id == guard && c.NodeId == 3));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Contract, Type = "契约时魔" },
            new EffectInstruction { EffectId = "Summon", Target = "selected" }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, guard)); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, carrier));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
    }
    [Test] public void CoveringCarrierByOverclockReturnsPlayersButDoesNotDestroyThem()
    {
        AttachmentSetup(); Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Modify, Target = "all", Zone = TestCardZone.Hand, Stat = "time", Amount = 1 } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(), 0));
        Assert.AreEqual(4, match.PlayerCards.Length);
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
    }
    [Test] public void RangeModifierAllowsGraphDistanceTwoButNotContractMovementDistanceTwo()
    {
        AttachmentSetup(); provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Modify, Stat = "range", Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(2, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).AttackRange);
        Assert.IsFalse(Act(TestCommandKind.MoveContract, Contract(), 1));
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Owner == 1));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void AttachedPlayerCannotBeAttackedUntilCarrierAndHomeGuardAreRemoved()
    {
        AttachmentSetup(); var guard = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, guard, 3));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        var carrierKiller = Contract(1);
        Assert.IsTrue(Act(TestCommandKind.MoveContract, carrierKiller, 2));
        var guardKiller = Hand(owner: 1); Assert.IsTrue(Act(TestCommandKind.Summon, guardKiller, 1));
        var playerAttacker = Hand(owner: 1); Assert.IsTrue(Act(TestCommandKind.Summon, playerAttacker, 4));
        Phase(TestTurnPhase.Combat);
        Assert.IsFalse(Act(TestCommandKind.AttackPlayer, playerAttacker, 3));
        Assert.IsTrue(Act(TestCommandKind.Attack, carrierKiller, 0));
        Assert.IsTrue(Act(TestCommandKind.Stay));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 3 && c.AttachedTo == Guid.Empty));
        Assert.IsFalse(Act(TestCommandKind.AttackPlayer, playerAttacker, 3));
        Assert.IsTrue(Act(TestCommandKind.Attack, guardKiller, 3));
        Assert.IsTrue(Act(TestCommandKind.AttackPlayer, playerAttacker, 3));
        Assert.AreEqual(5, match.ForPlayer(0).DamagePointers[0]);
    }
    [Test] public void AttachmentAloneBlocksAnOtherwiseExposedHomePlayer()
    {
        AttachmentSetup(); Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Combat);
        Assert.IsFalse(Act(TestCommandKind.AttackPlayer, Contract(1), 3));
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[0]);
        Assert.IsFalse(match.ForPlayer(0).Board.Single(c => c.Owner == 1).Tapped);
    }
    [Test] public void AttachmentEstablishedByAttackTriggerCancelsPendingPlayerAttack()
    {
        AttachmentSetup();
        Listen("contract", new Mishi.Battle.Events.EventSubscription { Id = "protect", Event = "AttackDeclared", Side = "opponent" });
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { EffectId = "AttachPlayers" } };
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.AttackPlayer, Contract(1), 3));
        Assert.AreEqual(0, match.ChoicePlayer);
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id, player: 0));
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[0]);
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.AttachedTo == Contract()));
    }
}
