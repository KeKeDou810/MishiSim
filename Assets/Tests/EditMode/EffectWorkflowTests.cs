using System;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [Test] public void PlayerFrontFaceRevivesContractPaysThreeAndFlipsAgain()
    {
        Setup(); var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2); board.SetPlayerOrDefenseNode(3, 1);
        match = new NetworkTestMatch(board, new[] { 0, 1, 2, 3 }, 0, 1, "contract", "normal");
        provider.Definitions["front"] = new CardEffectRules { Name = "Front", Type = "玩家卡", HasActivate = true, ActivateZone = TestCardZone.Player };
        provider.Definitions["back"] = new CardEffectRules { Name = "Back", Type = "玩家卡", HasActivate = true, ActivateZone = TestCardZone.Player,
            ActivationCosts = new[] { new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 3, Maximum = 3 } } };
        match.AttachEffects(provider, ClockKind.White, ClockKind.Black);
        match.AddPlayerCards(0, 0, new NetworkTestMatch.Card(Guid.NewGuid(), "front", 0, 0), new NetworkTestMatch.Card(Guid.NewGuid(), "back", 0, 0));
        sequence = 0; Phase(TestTurnPhase.Main);
        var front = match.PlayerCards.Single(c => c.DefinitionId == "front").Id;
        var back = match.PlayerCards.Single(c => c.DefinitionId == "back").Id;
        Assert.IsTrue(match.CanActivateCard(front)); Assert.IsFalse(Act(TestCommandKind.ActivateEffect, back));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Destroy } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.PlayerCards.Single(c => c.Id == front).Covered);
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Contract, Type = "契约时魔" },
            new EffectInstruction { EffectId = "Summon", Target = "selected" }
        } : Array.Empty<EffectInstruction>();
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, front)); Assert.IsTrue(Act(TestCommandKind.ActivateEffect, back));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates.Single()));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 3));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        Assert.AreEqual(1, match.ForPlayer(0).CostPointers[0]);
        Assert.IsFalse(match.PlayerCards.Single(c => c.Id == front).Covered);
        Assert.IsTrue(match.PlayerCards.Single(c => c.Id == back).Covered);
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Owner == 0 && c.IsContract && c.NodeId == 2 && !c.Tapped));
    }
    [Test] public void NextBattleScheduleResolvesBeforeCombat()
    {
        Setup(); provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { EffectId = "Schedule", Timing = "nextBattle", After = new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "battle", Value = EffectValue.Integer(1) }
        } } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.AreEqual(1, match.ForPlayer(0).Variables.Single(v => v.Key == "battle").Value.RequireInteger());
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void CompositePaymentCanCancelWithoutSpendingOrConsumingLimit()
    {
        Setup(); var source = Contract(); int hand = match.ForPlayer(0).OwnHand.Length;
        provider.Definitions["contract"].ActivationCosts = new[] {
            new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 1, Maximum = 3, StoreAs = "paid" },
            new ActivationCost { Kind = ActivationCostKind.Discard, Minimum = 2, Maximum = 2 }
        };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, source));
        Assert.IsEmpty(match.ForPlayer(1).Choice.Candidates);
        Assert.IsFalse(Act(TestCommandKind.ChooseNumber, node: 2, player: 1));
        Assert.IsFalse(Act(TestCommandKind.ChooseNumber, node: 4));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 2));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Hand()));
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(hand, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Guid.Empty));
        Assert.IsTrue(match.CanActivateCard(source));
        Assert.AreEqual(hand, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void CompositePaymentCommitsOnlyAfterAllSelectionsAndStoresPaidX()
    {
        Setup();
        provider.Definitions["contract"].ActivationCosts = new[] {
            new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 1, Maximum = 3, StoreAs = "paid" },
            new ActivationCost { Kind = ActivationCostKind.Discard, Minimum = 2, Maximum = 2 }
        };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "paid", ValueReference = new VariableReference { Scope = VariableScope.Effect, Key = "paid" } } };
        int hand = match.ForPlayer(0).OwnHand.Length;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(match.AdvanceTime(5));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 2));
        Assert.AreEqual(15, match.ChoiceSeconds);
        var first = match.ForPlayer(0).Choice.Candidates[0];
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, first));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffect, first));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates[0]));
        Assert.AreEqual(hand, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(hand - 2, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(2, match.ForPlayer(0).Variables.Single(v => v.Key == "paid").Value.RequireInteger());
        Assert.IsFalse(match.CanActivateCard(Contract()));
    }
    [Test] public void PaymentTimeoutCancelsAndImpossibleCombinedPaymentIsRejected()
    {
        Setup(); provider.Definitions["contract"].ActivationCosts = new[] {
            new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 3, Maximum = 3 },
            new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 2, Maximum = 2 }
        };
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract())); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        provider.Definitions["contract"].ActivationCosts = new[] { new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 1, Maximum = 3 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(match.AdvanceTime(21));
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void SacrificeCostCanDestroyItsSourceAndStillResolveThePaidEffect()
    {
        Setup(); var source = Contract();
        provider.Definitions["contract"].ActivationCosts = new[] { new ActivationCost { Kind = ActivationCostKind.Destroy, Minimum = 1, Maximum = 1 } };
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "resolved", Value = EffectValue.Bool(true) } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, source));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, source));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == source));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Id == source));
        Assert.IsTrue(match.ForPlayer(0).Variables.Single(v => v.Key == "resolved").Value.Boolean);
    }
    [Test] public void EffectSummonPreservesOriginPublishesDistinctReasonAndDoesNotPayPrintedTime()
    {
        Setup(); var unit = Hand(); string from = null, reason = null;
        Listen("contract", new EventSubscription { Id = "watch", Event = "Summoned", Subject = "other", Side = "own" });
        provider.ListenerPlan = (c, _) => { from = ((SummonedEvent)c.TriggerEvent).From.ToString(); reason = c.TriggerEvent.Reason; return Array.Empty<EffectInstruction>(); };
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand, Type = "通常时魔" },
            new EffectInstruction { EffectId = "Summon", Target = "selected" }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, unit));
        Assert.IsTrue(match.ForPlayer(0).Choice.IsBoardPlacement);
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 0));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 2, player: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        Assert.AreEqual("Hand", from); Assert.AreEqual("effectSummon", reason);
        Assert.IsFalse(match.ForPlayer(0).Board.Single(c => c.Id == unit).Tapped);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void DelayedReturnSurvivesSourceDepartureButNotTargetReentry()
    {
        Setup(); var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Definitions["normal"].HasActivate = true;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Choose, Type = "通常时魔" },
            new EffectInstruction { EffectId = "Schedule", Target = "selected", After = new[] { new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "set", FromSet = "scheduled", Position = "bottom" } } },
            new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, unit));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.IsContract && c.Owner == 0));
        Phase(TestTurnPhase.End);
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Id == unit));
        Assert.AreEqual(-1, match.Winner);

        Setup(); unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2)); provider.Definitions["normal"].HasActivate = true;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { EffectId = "Schedule", After = new[] { new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "set", FromSet = "scheduled", Position = "bottom" } } },
            new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit)); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Phase(TestTurnPhase.End); Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == unit));
    }
    [Test] public void NextOwnMainScheduleWaitsThroughOpponentTurnAndCopiesEffectVariables()
    {
        Setup(); provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Key = "x", Value = EffectValue.Integer(7) },
            new EffectInstruction { EffectId = "Schedule", Timing = "nextOwnMain", After = new[] {
                new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "result", ValueReference = new VariableReference { Key = "x" } }
            } }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "result"));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.AreEqual(7, match.ForPlayer(0).Variables.Single(v => v.Key == "result").Value.RequireInteger());
    }
    [Test] public void TurnTimeoutRunsEndSchedulesBeforePassingTheTurn()
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { EffectId = "Schedule", After = new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "ended", Value = EffectValue.Bool(true) }
        } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(match.AdvanceTime(121));
        Assert.AreEqual(1, match.ActivePlayer); Assert.IsTrue(match.ForPlayer(0).Variables.Single(v => v.Key == "ended").Value.Boolean);
    }
}
