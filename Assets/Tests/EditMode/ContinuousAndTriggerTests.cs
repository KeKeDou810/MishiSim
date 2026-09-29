using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [Test] public void DynamicContributionsStackRefreshAndRemoveOnlyTheirSource()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var normal = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, normal, 2));
        provider.ContinuousPlan = c => c.Owner != 0 ? Array.Empty<ContinuousModifier>() : new[] {
            new ContinuousModifier { Id = "counter_power", Target = "all", Amount =
                c.Variables[VariableScope.Card].TryGetValue("counter", out var value) ? value.RequireInteger() * 100 : 0 }
        };
        provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card,
            Key = "counter", Value = EffectValue.Integer(c.DefinitionId == "contract" ? 2 : 3) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(1500, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.AddValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(1600, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(1200, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, normal, 2));
        Assert.AreEqual(1200, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void ContinuousConditionsDisableAndFinalClampIsOrderIndependent()
    {
        Setup();
        provider.ContinuousPlan = c => new[] { new ContinuousModifier { Id = "aura", Target = "all", Side = "any",
            Amount = c.Owner == 0 ? -2000 : 1500 } };
        Assert.AreEqual(500, match.ForPlayer(0).Board.First().Power);
        provider.ContinuousPlan = c => c.Owner == c.ActivePlayer ? new[] {
            new ContinuousModifier { Id = "own_turn", Amount = 100, Stat = "time" }
        } : Array.Empty<ContinuousModifier>();
        Assert.AreEqual(100, match.ForPlayer(0).Board.Single(c => c.Owner == 0).Time);
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(0, match.ForPlayer(0).Board.Single(c => c.Owner == 0).Time);
        Assert.AreEqual(100, match.ForPlayer(0).Board.Single(c => c.Owner == 1).Time);
    }
    [Test] public void SourceBoundModifiersDoNotReviveAfterReentryAndTurnModifiersRemain()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var normal = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, normal, 2));
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Modify, Target = "all", Type = "契约时魔", Amount = 300, Duration = "source" },
            new EffectInstruction { Op = EffectOp.Modify, Target = "all", Type = "契约时魔", Amount = 200 }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(1500, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(1200, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, normal, 2));
        Assert.AreEqual(1200, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
    }
    private void AcceptPendingTriggers()
    {
        for (int i = 0; i < 128 && match.ChoicePlayer >= 0; i++)
        {
            var choice = match.ForPlayer(match.ChoicePlayer).Choice;
            if (choice.TriggerOptions.Length == 0) return;
            Assert.IsTrue(Act(TestCommandKind.OrderTrigger, choice.TriggerOptions[0].Id, player: choice.Player));
        }
    }
    private AutomaticEffectPlan Trigger(string id, string value) => new AutomaticEffectPlan { Id = id, Label = id,
        Steps = new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Match, Key = "last", Value = EffectValue.String(value), Visibility = "public" } } };
    [Test] public void DeclinedSingleTriggerDoesNotConsumeItsOncePerTurnLimit()
    {
        Setup();
        Listen("contract", new Mishi.Battle.Events.EventSubscription { Id = "summon", Event = "Summoned", Side = "own", OncePerTurn = true });
        provider.ListenerPlan = (_, __) => Trigger("value", "accepted").Steps;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.IsFalse(Act(TestCommandKind.OrderTrigger, Guid.Empty, player: 1));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "last"));
        provider.Definitions["normal"].HasActivate = true;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.AreEqual(1, match.ForPlayer(0).Choice.TriggerOptions.Length);
        AcceptPendingTriggers();
        Assert.AreEqual("accepted", match.ForPlayer(0).Variables.Single(v => v.Key == "last").Value.Text);
    }
    [Test] public void TimeoutDeclinesOnlyUnselectedEffectsAndPreservesAcceptedOrder()
    {
        Setup();
        provider.AutomaticPlan = _ => new[] { Trigger("a", "a"), Trigger("b", "b") };
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single(t => t.Label == "b").Id));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "last"));
        Assert.IsTrue(match.AdvanceTime(21));
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual("b", match.ForPlayer(0).Variables.Single(v => v.Key == "last").Value.Text);
    }
    [Test] public void DecliningTurnPlayersTriggersStillOffersOpponentsTriggers()
    {
        Setup();
        provider.AutomaticPlan = c => c.Event == EffectEvent.Destroyed ? new[] { Trigger("one", c.Owner.ToString()) } : Array.Empty<AutomaticEffectPlan>();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "any" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty));
        Assert.AreEqual(1, match.ChoicePlayer);
        Assert.IsFalse(Act(TestCommandKind.OrderTrigger, Guid.Empty, player: 0));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty, player: 1));
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "last"));
    }
    [TestCase(0)] [TestCase(1)] public void BothControllersOrderTheirBatchBeforeAnyResolutionActivePlayerFirst(int active)
    {
        Setup();
        if (active == 1) { Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main); }
        provider.AutomaticPlan = c => c.Event == EffectEvent.Destroyed ? new[] { Trigger("a", c.Owner + "a"), Trigger("b", c.Owner + "b") } : Array.Empty<AutomaticEffectPlan>();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "any" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract(active)));
        Assert.AreEqual(active, match.ChoicePlayer);
        Assert.IsEmpty(match.ForPlayer(1 - active).Choice.TriggerOptions);
        var first = match.ForPlayer(active).Choice.TriggerOptions.Single(t => t.Label == "b").Id;
        Assert.IsFalse(Act(TestCommandKind.OrderTrigger, first, player: 1 - active));
        Assert.IsFalse(Act(TestCommandKind.OrderTrigger, Guid.NewGuid()));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, first));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(active).Choice.TriggerOptions.Single().Id, player: active));
        Assert.AreEqual(1 - active, match.ChoicePlayer);
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "last"));
        Assert.IsFalse(Act(TestCommandKind.OrderTrigger, first, player: 1 - active));
        var second = match.ForPlayer(1 - active).Choice.TriggerOptions.Single(t => t.Label == "b").Id;
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, second, player: 1 - active));
        AcceptPendingTriggers();
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual((1 - active) + "a", match.ForPlayer(0).Variables.Single(v => v.Key == "last").Value.Text);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void TriggerSortingTimeoutSharesBudgetAndAllowsOtherControllerTheirOwnTime()
    {
        Setup();
        provider.AutomaticPlan = c => c.Event == EffectEvent.Destroyed ? new[] { Trigger("a", c.Owner + "a"), Trigger("b", c.Owner + "b"), Trigger("c", c.Owner + "c") } : Array.Empty<AutomaticEffectPlan>();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "any" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(match.AdvanceTime(7));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions[1].Id));
        Assert.AreEqual(13, match.ChoiceSeconds);
        Assert.IsTrue(match.AdvanceTime(14));
        Assert.AreEqual(1, match.ChoicePlayer); Assert.AreEqual(20, match.ChoiceSeconds);
        Assert.IsTrue(match.AdvanceTime(21));
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void SingleTriggerRequiresAcceptanceAndItsTargetChoiceResumesNormally()
    {
        Setup();
        provider.AutomaticPlan = _ => new[] { new AutomaticEffectPlan { Id = "pick", Steps = new[] {
            new EffectInstruction { Op = EffectOp.Choose, Type = "契约时魔" },
            new EffectInstruction { Op = EffectOp.Modify, Target = "selected", Amount = 100 }
        } } };
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.AreEqual(1, match.ForPlayer(0).Choice.TriggerOptions.Length);
        AcceptPendingTriggers();
        Assert.IsEmpty(match.ForPlayer(0).Choice.TriggerOptions);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.AreEqual(1100, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void PriorityFollowsTurnPlayerRatherThanHostAndTriggeredBatchWaits()
    {
        Setup(); Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        provider.AutomaticPlan = c => c.Owner == 1 ? new[] {
            new AutomaticEffectPlan { Id = "destroy", Steps = new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "opponent" } } },
            Trigger("last", "original_batch")
        } : new[] { Trigger("later", "triggered_batch") };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract(1)));
        Assert.AreEqual(1, match.ChoicePlayer);
        var first = match.ForPlayer(1).Choice.TriggerOptions.Single(t => t.Label == "Destroyed").Id;
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, first, player: 1));
        AcceptPendingTriggers();
        Assert.AreEqual("triggered_batch", match.ForPlayer(0).Variables.Single(v => v.Key == "last").Value.Text);
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void TriggerConditionsSeeEntireSimultaneousDestruction()
    {
        Setup();
        provider.AutomaticPlan = c => c.OwnFieldNameCounts.ContainsKey("Contract") ? new[] { Trigger("bad", "still_on_board") } : new[] { Trigger("good", "gone") };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "any" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        AcceptPendingTriggers();
        Assert.AreEqual("gone", match.ForPlayer(0).Variables.Single(v => v.Key == "last").Value.Text);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void CoveredSourceStopsDynamicAndSourceBoundContributions()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var lower = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, lower, 2));
        provider.ContinuousPlan = c => c.DefinitionId == "normal" ? new[] { new ContinuousModifier { Id = "aura", Target = "all", Amount = 300 } } : Array.Empty<ContinuousModifier>();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Modify, Target = "all", Type = "契约时魔", Duration = "source", Amount = 200 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, lower));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Modify, Target = "all", Zone = TestCardZone.Hand, Type = "通常时魔", Stat = "time", Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Overclock, Hand(), 2));
        // Only the new top card's aura remains; the covered source and its fixed modifier are inactive.
        Assert.AreEqual(1300, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void MalformedContinuousScriptStopsMatchWithoutLeakingAnException()
    {
        Setup(); provider.ContinuousPlan = _ => new[] { new ContinuousModifier { Id = "same" }, new ContinuousModifier { Id = "same" } };
        Assert.DoesNotThrow(() => match.ForPlayer(0));
        Assert.AreEqual(2, match.Winner);
    }
}
