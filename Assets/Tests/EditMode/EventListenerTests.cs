using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    private void Listen(string definition, params EventSubscription[] subscriptions) => provider.Listeners[definition] = subscriptions;
    [Test] public void OtherFriendlySummonUpdatesListenerCounterAndBuffsEventSubject()
    {
        Setup();
        Listen("contract", new EventSubscription { Id = "ally", Event = "Summoned", Subject = "other", Side = "own" });
        provider.ListenerPlan = (c, _) => new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) },
            new EffectInstruction { Op = EffectOp.Modify, Target = "event", Amount = 500 }
        };
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        AcceptPendingTriggers();
        Assert.AreEqual(1500, match.ForPlayer(0).Board.Single(c => c.Id == unit).Power);
        Assert.AreEqual(Contract(), match.ForPlayer(0).Variables.Single(v => v.Key == "counter").CardId);
        Assert.IsFalse(match.ForPlayer(1).Variables.Any(v => v.Key == "counter"));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void SimultaneouslyDestroyedListenersStillObserveOtherDestroyedCards()
    {
        Setup(); int observations = 0;
        Listen("contract", new EventSubscription { Id = "witness", Event = "Destroyed", Subject = "other" });
        provider.ListenerPlan = (c, _) => { observations++; return new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "seen", Value = EffectValue.Bool(true) } }; };
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Destroy, Target = "all", Side = "any" } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        AcceptPendingTriggers();
        Assert.AreEqual(2, observations);
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "seen"));
        Assert.IsTrue(match.ForPlayer(1).Variables.Any(v => v.Key == "seen"));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void ListenersStopAfterDepartureAndObserveAgainOnReentry()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true; int seen = 0;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Listen("normal", new EventSubscription { Id = "draw", Event = "Drawn", Side = "own" });
        provider.ListenerPlan = (_, __) => { seen++; return Array.Empty<EffectInstruction>(); };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.AreEqual(0, seen);
        provider.Plan = _ => Array.Empty<EffectInstruction>(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit)); Assert.AreEqual(1, seen);
    }
    [Test] public void EventTargetDoesNotFollowAReenteredInstance()
    {
        Setup(); var unit = Hand();
        Listen("contract", new EventSubscription { Id = "summon", Event = "Summoned", Side = "own" });
        provider.ListenerPlan = (_, __) => new[] {
            new EffectInstruction { Op = EffectOp.Move, Target = "event", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Modify, Target = "event", Amount = 1000 }
        };
        Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        AcceptPendingTriggers();
        Assert.AreEqual(1000, match.ForPlayer(0).OwnHand.Single(c => c.Id == unit).Power);
    }
    [Test] public void AutomaticTurnProgressWaitsForTriggersAndTheirTargetSelection()
    {
        Setup();
        Listen("contract", new EventSubscription { Id = "end_choice", Event = "TurnEnded", Side = "own" });
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { Op = EffectOp.Choose, Type = "契约时魔" } };
        Phase(TestTurnPhase.End);
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(0, match.ActivePlayer);
        Assert.AreEqual(TestTurnPhase.End, match.Phase);
        Assert.IsNotNull(match.ForPlayer(0).Choice);
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        AcceptPendingTriggers();
        Assert.IsFalse(match.AdvanceAutomaticPhases());
        Assert.AreEqual(0, match.ActivePlayer);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.AreEqual(1, match.ActivePlayer);
        Assert.IsTrue(match.AdvanceAutomaticPhases());
        Assert.AreEqual(TestTurnPhase.Main, match.Phase);
    }
    [Test] public void PhaseAndTurnTransitionsWaitUntilTheirListenersResolve()
    {
        Setup();
        Listen("contract", new EventSubscription { Id = "end", Event = "TurnEnded", Side = "own" },
            new EventSubscription { Id = "start", Event = "TurnStarted", Side = "own" });
        var order = new List<string>();
        provider.ListenerPlan = (c, id) => {
            order.Add(id + ":" + c.ActivePlayer);
            return id == "end" ? new[] { new EffectInstruction { Op = EffectOp.Choose, Type = "契约时魔" } } : Array.Empty<EffectInstruction>();
        };
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(0, match.ActivePlayer); Assert.AreEqual(TestTurnPhase.End, match.Phase);
        AcceptPendingTriggers();
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.AreEqual(1, match.ActivePlayer); CollectionAssert.AreEqual(new[] { "end:0", "start:1" }, order);
    }
    [Test] public void AttackListenerCanRemoveAttackerBeforeCombatContinues()
    {
        Setup();
        Listen("contract", new EventSubscription { Id = "attack", Event = "AttackDeclared", Side = "own" });
        provider.ListenerPlan = (c, _) => new[] { new EffectInstruction { Op = EffectOp.Move, Target = "event", Zone = TestCardZone.Hand } };
        Phase(TestTurnPhase.Combat); var opponent = Contract(1);
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == opponent));
        AcceptPendingTriggers();
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void DrawIdentityAndPrivateVariablesAreFilteredForOpponentListeners()
    {
        Setup(); var seen = new List<int>();
        Listen("contract", new EventSubscription { Id = "variable", Event = "VariableChanged", Key = "counter" },
            new EventSubscription { Id = "draw", Event = "Drawn", Name = "Normal" });
        provider.ListenerPlan = (c, _) => { seen.Add(c.Owner); return Array.Empty<EffectInstruction>(); };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) },
            new EffectInstruction { Op = EffectOp.Draw, Amount = 3 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsNotEmpty(seen); Assert.IsTrue(seen.All(p => p == 0));
    }
    [Test] public void UnchangedVariableDoesNotTriggerAndRepeatedListenerCanBeDeclined()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        int seen = 0;
        Listen("contract", new EventSubscription { Id = "counter", Event = "VariableChanged", Subject = "self", Key = "counter" });
        provider.ListenerPlan = (c, _) => { seen++; return Array.Empty<EffectInstruction>(); };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.AreEqual(1, seen);
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { Op = EffectOp.AddValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) } };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.AddValue, Scope = VariableScope.Card, Key = "counter", Value = EffectValue.Integer(1) } };
        Assert.DoesNotThrow(() => Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsNotNull(match.ForPlayer(0).Choice);
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty));
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void DamageHealingAndCostEventsCarryActualChanges()
    {
        Setup(); var changes = new List<BattleEvent>();
        Listen("contract", new EventSubscription { Id = "damage", Event = "DamageTaken", Side = "own" },
            new EventSubscription { Id = "heal", Event = "Healed", Side = "own" }, new EventSubscription { Id = "cost", Event = "CostChanged", Side = "own" });
        provider.ListenerPlan = (c, _) => { changes.Add(c.TriggerEvent); return Array.Empty<EffectInstruction>(); };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Damage, Amount = 1 }, new EffectInstruction { Op = EffectOp.Heal, Amount = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(1, ((DamageTakenEvent)changes.Single(e => e.Id == "DamageTaken")).Amount);
        Assert.AreEqual(1, ((HealedEvent)changes.Single(e => e.Id == "Healed")).Amount);
        Assert.AreEqual(0, changes.Count(e => e.Id == "CostChanged"));
    }
    [TestCase("exact", "Normal", true)]
    [TestCase("exact", "Norm", false)]
    [TestCase("fuzzy", "norm", true)]
    [TestCase("fuzzy", "Other", false)]
    public void SummonFiltersCombineCardTypeAndNameMode(string mode, string name, bool expected)
    {
        Setup(); int count = 0;
        Listen("contract", new EventSubscription { Id = "filter", Event = "Summoned", Side = "own", Type = "通常时魔", Name = name, NameMatch = mode });
        provider.ListenerPlan = (_, __) => { count++; return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.AreEqual(expected ? 1 : 0, count);
    }
    [Test] public void PlayAndActivateHaveIndependentTypeFilters()
    {
        Setup(); var seen = new List<string>();
        Listen("contract", new EventSubscription { Id = "play", Event = "Played", Side = "own", Types = new[] { "决策卡", "玩家卡" } },
            new EventSubscription { Id = "wrong", Event = "Played", Side = "own", Type = "通常时魔" },
            new EventSubscription { Id = "activate", Event = "Activated", Side = "own", Type = "契约时魔" });
        provider.ListenerPlan = (_, id) => { seen.Add(id); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true)));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        CollectionAssert.AreEqual(new[] { "play", "activate" }, seen);
    }
    [Test] public void EventFiltersRejectAmbiguousTypeAndUnknownNameMode()
    {
        Assert.Throws<FormatException>(() => new EventSubscription { Id = "x", Event = "Summoned", NameMatch = "typo" }.Validate());
        Assert.Throws<FormatException>(() => new EventSubscription { Id = "x", Event = "Played", Type = "决策卡", Types = new[] { "通常时魔" } }.Validate());
        Assert.IsTrue(BattleEventRegistry.Definitions.ContainsKey("VariableChanged"));
    }
}
