using System;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [Test] public void EffectLabSetupRejectsInvalidBoardAndDoesNotBypassLaterActionRules()
    {
        Setup();
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2); board.SetPlayerOrDefenseNode(1, 1);
        var scenario = new EffectTestScenario { Phase = TestTurnPhase.Draw, Cards = new[] {
            new EffectTestCard { Id = "contract", Owner = 0, Zone = TestCardZone.Board },
            new EffectTestCard { Id = "contract", Owner = 1, Zone = TestCardZone.Board },
            new EffectTestCard { Id = "normal", Owner = 0, Zone = TestCardZone.Deck, Count = 5 },
            new EffectTestCard { Id = "normal", Owner = 1, Zone = TestCardZone.Deck, Count = 5 },
            new EffectTestCard { Id = "normal", Owner = 0, Zone = TestCardZone.Hand }
        } };
        var lab = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, scenario, provider, new[] { 0 });
        var hand = lab.ForPlayer(0).OwnHand.Single();
        Assert.IsFalse(lab.TryCommand(0, lab.MatchId, 1, lab.Revision, TestCommandKind.Summon, hand.Id, 2, out _));
        Assert.IsEmpty(lab.ForPlayer(1).OwnHand);
        Assert.AreEqual(1, lab.ForPlayer(1).OpponentHandCount);
        scenario.Cards[4].Zone = TestCardZone.Board; scenario.Cards[4].Node = 1;
        Assert.Throws<ArgumentException>(() => NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, scenario, provider, new[] { 0 }));
        scenario.Cards[4].Node = 0;
        Assert.Throws<ArgumentException>(() => NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, scenario, provider, new[] { 0 }));
    }
    [Test] public void DecisionCompositeCostExcludesPlayedCardAndCancelsAtomically()
    {
        Setup(); var decision = Hand(true); int count = match.ForPlayer(0).OwnHand.Length;
        provider.Definitions["decision"].PlayCosts = new[] {
            new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 2, Maximum = 2 },
            new ActivationCost { Kind = ActivationCostKind.Discard, Minimum = 1, Maximum = 1 } };
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, decision));
        Assert.IsFalse(match.ForPlayer(0).Choice.Candidates.Contains(decision));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Guid.Empty));
        Assert.AreEqual(count, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]); Assert.AreEqual(0, match.DecisionsUsed);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, decision));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Hand()));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(count - 2, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]); Assert.AreEqual(1, match.DecisionsUsed);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void ResponseDecisionPaysBeforePassingPriority()
    {
        Setup(true); provider.Definitions["decision"].OwnTurnOnly = false;
        provider.Definitions["decision"].PlayCosts = new[] { new ActivationCost { Minimum = 1, Maximum = 1 } };
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true, 1), player: 1));
        Assert.AreEqual(1, match.ResponsePlayer);
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: -1, player: 1));
        Assert.AreEqual(1, match.ResponsePlayer); Assert.AreEqual(4, match.ForPlayer(1).CostPointers[1]);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true, 1), player: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0, player: 1));
        Assert.AreEqual(0, match.ResponsePlayer); Assert.AreEqual(3, match.ForPlayer(1).CostPointers[1]);
    }
    [Test] public void TriggerPaymentTimeoutSkipsOnlyThatEffectAndResumesQueue()
    {
        Setup();
        provider.Listeners["contract"] = new[] { new EventSubscription { Id = "paid", Event = "Summoned", Side = "own", Costs = new[] { new ActivationCost { Minimum = 1, Maximum = 1 } } } };
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { Op = EffectOp.SetValue, Key = "paidTrigger", Scope = VariableScope.Player, Value = EffectValue.Integer(1) } };
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2)); AcceptPendingTriggers();
        Assert.IsTrue(match.ForPlayer(0).Choice.IsPayment);
        Assert.IsTrue(match.AdvanceTime(21));
        Assert.AreEqual(-1, match.ChoicePlayer); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "paidTrigger"));
        Assert.IsTrue(Act(TestCommandKind.NextPhase));
    }
    [Test] public void TriggerPaymentStoresVariableBeforeInstructions()
    {
        Setup();
        provider.AutomaticPlan = c => c.Event == EffectEvent.Summoned ? new[] { new AutomaticEffectPlan {
            Id = "paid", Costs = new[] { new ActivationCost { Minimum = 1, Maximum = 3, StoreAs = "paid" } },
            Steps = new[] { new EffectInstruction { Op = EffectOp.SetValue, Key = "result", Scope = VariableScope.Player, ValueReference = new VariableReference { Key = "paid" } } }
        } } : Array.Empty<AutomaticEffectPlan>();
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2)); AcceptPendingTriggers();
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 2));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(2, match.ForPlayer(0).Variables.Single(v => v.Key == "result").Value.RequireInteger());
    }
    [TestCase("nextBattle", true)] [TestCase("nextDefend", true)] [TestCase("nextAttack", false)]
    public void BattleSchedulesDistinguishDefenderAndAttacker(string timing, bool fires)
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "Schedule", Target = "all", Side = "opponent", Timing = timing,
            After = new[] { new EffectInstruction { Op = EffectOp.SetValue, Key = "defended", Scope = VariableScope.Player, Value = EffectValue.Bool(true) } } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.AreEqual(fires, match.ForPlayer(0).Variables.Any(v => v.Key == "defended"));
    }
    [Test] public void ContinuousRangeStacksAndLosesOnlyDepartingSource()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        Guid normal = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, normal, 2));
        provider.ContinuousPlan = c => c.Owner == 0 ? new[] { new ContinuousModifier { Id = "reach", Stat = "range", Target = "all", Amount = 1 } } : Array.Empty<ContinuousModifier>();
        Assert.AreEqual(3, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).AttackRange);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.AreEqual(2, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).AttackRange);
    }
    [TestCase(false)] [TestCase(true)] public void ReturningOverclockPreservesSpecialContractExitWithoutDestroyEvents(bool deck)
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        provider.ContinuousPlan = c => c.DefinitionId == "contract" && c.Owner == 0 ? new[] { new ContinuousModifier { Id = "zero", Stat = "time", Amount = 1 } } : Array.Empty<ContinuousModifier>();
        // Increase the hand card's time through a direct effect, then overclock onto the contract.
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Modify, Target = "all", Zone = TestCardZone.Hand, Type = "通常时魔", Stat = "time", Amount = 2 } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var normal = Hand(); Assert.IsTrue(Act(TestCommandKind.Overclock, normal, 0));
        provider.Plan = _ => new[] { deck ? new EffectInstruction { Op = EffectOp.ReturnToDeck, Position = "bottom" } : new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        int before = match.ForPlayer(0).DeckCounts[0];
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, normal));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.NodeId == 0));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.IsContract && c.Owner == 0 && c.Zone == TestCardZone.Contract));
        if (deck) Assert.AreEqual(before, match.ForPlayer(0).DeckCounts[0]); // returned one, contract exit draws one
        else Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == normal));
        Assert.IsFalse(match.ActionLogFor(0).Any(s => s.Contains("被破坏")));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void PrivateScryNamesStayOutOfOpponentsActionLog()
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 2 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.ActionLogFor(0).Any(s => s.Contains("查看卡片：")));
        Assert.IsFalse(match.ActionLogFor(1).Any(s => s.Contains("查看卡片：")));
        Assert.IsTrue(match.ActionLogFor(1).Any(s => s.Contains("查看牌库 2 张卡")));
    }
    [TestCase(false)] [TestCase(true)] public void ReturningOverclockMovesOnlyTopAndDiscardsLowerOrdinaryCards(bool deck)
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var lower = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, lower, 2));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Modify, Target = "all", Zone = TestCardZone.Hand, Type = "通常时魔", Stat = "time", Amount = 1 }
        } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var top = Hand(); Assert.IsTrue(Act(TestCommandKind.Overclock, top, 2));
        int deckCount = match.ForPlayer(0).DeckCounts[0];
        provider.Plan = _ => new[] { deck ? new EffectInstruction { Op = EffectOp.ReturnToDeck, Position = "bottom" } : new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, top));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.NodeId == 2));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == lower && c.Zone == TestCardZone.Discard));
        Assert.IsFalse(match.ForPlayer(0).OwnHand.Any(c => c.Id == lower));
        if (deck) Assert.AreEqual(deckCount + 1, match.ForPlayer(0).DeckCounts[0]);
        else Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == top));
        Assert.IsFalse(match.ActionLogFor(0).Any(s => s.Contains("被破坏")));
        Assert.AreEqual(-1, match.Winner);
    }
}
