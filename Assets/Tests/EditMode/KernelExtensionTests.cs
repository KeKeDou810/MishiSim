using System;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [TestCase(false)] [TestCase(true)]
    public void DiceContinuationUsesAuthoritativeResultAndPresentationAudience(bool reveal)
    {
        Setup(); int rolled = 0;
        provider.Plan = _ => new[] {
            new EffectInstruction { EffectId = "RollDice", Amount = 6, StoreAs = "die", Reveal = reveal },
            new EffectInstruction { EffectId = "Continue", Callback = "afterRoll" } };
        provider.ScryPlan = c => {
            rolled = c.Variables[VariableScope.Effect]["die"].RequireInteger();
            Assert.AreEqual(ClockKind.Black, c.OpponentClock);
            Assert.AreEqual(2, c.PublicCards.Count(card => card.Zone == TestCardZone.Board));
            return new[] { new EffectInstruction { Op = EffectOp.Modify, Amount = rolled * 100 } };
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.That(rolled, Is.InRange(1, 6));
        Assert.AreEqual(1000 + rolled * 100, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        var presentation = match.DrainPresentations().Single(p => p.Kind == CardPresentationKind.Dice);
        Assert.AreEqual(rolled, presentation.DiceResult); Assert.AreEqual(6, presentation.DiceSides);
        Assert.AreEqual(reveal ? -1 : 0, presentation.Audience);
        Assert.IsFalse(match.ForPlayer(1).Variables.Any(v => v.Key == "die"));
    }
    [Test] public void MultiSelectionStoresAllChosenCardsAndCanStopAtMinimum()
    {
        Setup(); int hand = match.ForPlayer(0).OwnHand.Length;
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand, MinimumCount = 1, MaximumCount = 3, StoreAs = "picked" },
            new EffectInstruction { Op = EffectOp.Move, Target = "set", FromSet = "picked", Zone = TestCardZone.Discard } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffect, Guid.Empty));
        var first = match.ForPlayer(0).Choice.Candidates[0]; Assert.IsTrue(Act(TestCommandKind.ChooseEffect, first));
        Assert.IsFalse(match.ForPlayer(0).Choice.Candidates.Contains(first));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Guid.Empty));
        Assert.AreEqual(hand - 1, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == first));
    }
    [Test] public void HiddenHandSelectionUsesAnonymousIdsAndRestoresOriginalOwnerOnExit()
    {
        Setup(); var enemyIds = match.ForPlayer(1).OwnHand.Select(c => c.Id).ToArray();
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "ChooseHiddenHand", StoreAs = "stolen" },
            new EffectInstruction { EffectId = "Continue", Callback = "privateCheck" },
            new EffectInstruction { EffectId = "AttachUnder", FromSet = "stolen" } };
        provider.ScryPlan = c => { Assert.IsEmpty(c.Sets["stolen"]); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var choice = match.ForPlayer(0).Choice;
        Assert.IsFalse(choice.Candidates.Any(enemyIds.Contains));
        Assert.IsTrue(choice.ViewedCards.All(c => string.IsNullOrEmpty(c.DefinitionId)));
        Assert.IsEmpty(match.ForPlayer(1).Choice.Candidates);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, choice.Candidates[0]));
        var attached = match.ForPlayer(0).Board.Single(c => c.HiddenAttachment);
        Assert.AreEqual("", attached.DefinitionId); Assert.IsTrue(attached.Covered);
        Assert.AreEqual(0, attached.Owner);
        Assert.IsFalse(string.IsNullOrEmpty(match.ForPlayer(1).Board.Single(c => c.Id == attached.Id).DefinitionId));
        provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Destroy } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var returned = match.ForPlayer(0).PublicPiles.Single(c => c.Id == attached.Id);
        Assert.AreEqual(1, returned.Owner); Assert.AreEqual(TestCardZone.Discard, returned.Zone);
        Assert.IsFalse(returned.HiddenAttachment);
    }
    [Test] public void DeckPositionEventIncludesSameDeckReorderAndAllowsSelfListenerInDeck()
    {
        Setup(); int calls = 0;
        provider.Listeners["normal"] = new[] { new EventSubscription { Id = "bottom", Event = "DeckPositioned", ActiveZone = TestCardZone.Deck, Subject = "self" } };
        provider.ListenerPlan = (c, _) => {
            Assert.AreEqual("bottom", ((DeckPositionedEvent)c.TriggerEvent).Position); calls++;
            return new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "bottomSeen", Value = EffectValue.Bool(true) } };
        };
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "QueryCards", Zone = TestCardZone.Deck, Type = "通常时魔", StoreAs = "normals" },
            new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "set", FromSet = "normals", Position = "bottom" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); AcceptPendingTriggers();
        Assert.Greater(calls, 0); Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "bottomSeen"));
        Assert.Throws<FormatException>(() => new EventSubscription { Id = "bad", Event = "CardMoved", ActiveZone = TestCardZone.Deck }.Validate());
    }
    [Test] public void BattleEndedAndDestroyedExposeTheAttacker()
    {
        Setup(); Guid killer = Contract(); bool destroyed = false, ended = false;
        provider.Listeners["contract"] = new[] {
            new EventSubscription { Id = "killed", Event = "Destroyed", ActiveZone = TestCardZone.Board },
            new EventSubscription { Id = "end", Event = "BattleEnded", ActiveZone = TestCardZone.Board } };
        provider.ListenerPlan = (c, id) => {
            if (id == "killed") { destroyed = true; Assert.AreEqual(killer, c.TriggerEvent.Cause.InstanceId); }
            else { ended = true; Assert.AreEqual(killer, ((BattleEndedEvent)c.TriggerEvent).Attacker.InstanceId); }
            return Array.Empty<EffectInstruction>();
        };
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, killer, 1));
        Assert.IsTrue(destroyed); Assert.IsTrue(ended);
    }
    [Test] public void BattleProtectionPreventsDestructionAndOccupation()
    {
        Setup(); Guid defender = Contract(1);
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "PreventDestruction", Target = "all", Side = "opponent", From = "battle" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Phase(TestTurnPhase.Combat);
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == defender));
        Assert.AreEqual(-1, match.ForPlayer(0).OccupationNode);
    }
    [Test] public void VariableFilteredDiscardPaymentCancelsBeforeCommitAndStoresCount()
    {
        Setup(); int hand = match.ForPlayer(0).OwnHand.Length;
        provider.Definitions["contract"].ActivationCosts = new[] { new ActivationCost { Kind = ActivationCostKind.Discard, Minimum = 0, Maximum = 2, Type = "决策卡", StoreAs = "paid" } };
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "paidCount", ValueReference = new VariableReference { Key = "paid" } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 2));
        Assert.IsTrue(match.ForPlayer(0).Choice.ViewedCards.All(c => c.IsDecision));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates[0]));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Guid.Empty));
        Assert.AreEqual(hand, match.ForPlayer(0).OwnHand.Length);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates[0]));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(hand - 1, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(1, match.ForPlayer(0).Variables.Single(v => v.Key == "paidCount").Value.RequireInteger());
    }
    [TestCase(ActivationCostKind.Exile)] [TestCase(ActivationCostKind.ReturnToDeck)]
    public void ExtraCostMovesChosenHandCardAfterConfirmation(ActivationCostKind kind)
    {
        Setup(); Guid chosen = Hand(); int count = match.ForPlayer(0).DeckCounts[0];
        provider.Definitions["contract"].ActivationCosts = new[] { new ActivationCost { Kind = kind, Zone = TestCardZone.Hand, Minimum = 1, Maximum = 1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, chosen));
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == chosen));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.IsFalse(match.ForPlayer(0).OwnHand.Any(c => c.Id == chosen));
        if (kind == ActivationCostKind.Exile) Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == chosen && c.Zone == TestCardZone.Exile));
        else Assert.AreEqual(count + 1, match.ForPlayer(0).DeckCounts[0]);
    }
    [Test] public void SpawnBoardKeepsChosenTimeAndRemoveDoesNotDestroy()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { EffectId = "SpawnBoard", DefinitionId = "token", Amount = 3 } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.ForPlayer(0).Choice.IsBoardPlacement);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        var token = match.ForPlayer(0).Board.Single(c => c.IsToken); Assert.AreEqual(3, token.Time); Assert.IsFalse(token.Tapped);
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "RemoveToken", Target = "all", Type = "衍生物" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.IsToken));
        Assert.IsFalse(match.ForPlayer(0).PublicPiles.Any(c => c.IsToken));
        Assert.IsFalse(match.ActionLogFor(0).Any(s => s.Contains("被破坏")));
    }
    [TestCase("Destroy")] [TestCase("Move")]
    public void TokenDestructionOrExileMakesItDisappearFromAllVisibleZones(string effect)
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { EffectId = "SpawnBoard", DefinitionId = "token" } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.IsToken));
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Board, Type = "衍生物" },
            new EffectInstruction { EffectId = effect, Target = "selected", Zone = TestCardZone.Exile }
        } : Array.Empty<EffectInstruction>();
        var tokenId = match.ForPlayer(0).Board.Single(c => c.IsToken).Id;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, tokenId));
        foreach (int owner in new[] { 0, 1 })
        {
            var view = match.ForPlayer(owner);
            Assert.IsFalse(view.Board.Concat(view.PublicPiles).Concat(view.OwnHand).Any(c => c.IsToken));
        }
        Assert.IsFalse(match.ForObserver().PublicPiles.Any(c => c.IsToken));
    }
    [Test] public void QuerySupportsTypeOrAndFuzzyNames()
    {
        Setup(); int count = 0;
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "QueryCards", Zone = TestCardZone.Hand, Types = new[] { "通常时魔", "决策卡" }, Name = "norm", NameMatch = "fuzzy", StoreAs = "found" },
            new EffectInstruction { EffectId = "Continue", Callback = "check" } };
        provider.ScryPlan = c => { count = c.Sets["found"].Length; Assert.IsTrue(c.Sets["found"].All(v => v.Name == "Normal")); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.Greater(count, 0);
    }
    [Test] public void GrantedTriggerIsOptionalAndRemovedOnLeaveReentry()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "GrantTrigger", Target = "all", Type = "通常时魔", Key = "CostChanged", Callback = "granted" }, new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        provider.ScryPlan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Key = "granted", Scope = VariableScope.Player, Value = EffectValue.Bool(true) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.ForPlayer(0).Choice.TriggerOptions.Length > 0);
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "granted"));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand }, new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void DecisionCostModifierChangesActualPayment()
    {
        Setup(); provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { EffectId = "ModifyDecisionCost", Amount = 2 } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true)));
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void ForesightCompletionCanBeCopiedOnceWithoutPublishingRecursiveCompletion()
    {
        Setup(); provider.Definitions["contract"].ForesightCount = 1;
        provider.Definitions["normal"].ForesightMark = provider.Definitions["decision"].ForesightMark = ForesightMark.Draw;
        provider.Listeners["contract"] = new[] { new EventSubscription { Id = "copy", Event = "ForesightResolved", Side = "own" } };
        int completions = 0;
        provider.ListenerPlan = (c, _) => { completions++; return new[] { new EffectInstruction { EffectId = "CopyForesight" } }; };
        int before = match.ForPlayer(0).OwnHand.Length;
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        AcceptPendingTriggers();
        Assert.AreEqual(before + 2, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(1, completions); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void InvocationRunsCopiedDecisionAndItsOwnContinuationWithoutPlayingIt()
    {
        Setup(); int count = match.ForPlayer(0).OwnHand.Length;
        provider.Plan = c => c.Event == EffectEvent.Played ? new[] { new EffectInstruction { EffectId = "Continue", Callback = "decisionTail" } } :
            new[] { new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand, Type = "决策卡" }, new EffectInstruction { EffectId = "InvokeDecision", Target = "selected" } };
        provider.ScryPlan = c => { Assert.AreEqual("decision", c.DefinitionId); return new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } }; };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates[0]));
        Assert.AreEqual(count + 1, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(0, match.DecisionsUsed); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void DestructionReplacementKeepsProtectedCardAndDestroysSubstitute()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Choose, Type = "通常时魔" },
            new EffectInstruction { EffectId = "ReplaceDestruction", From = "effect" }, new EffectInstruction { Op = EffectOp.Destroy } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, unit));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.IsContract && c.Owner == 0));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Id == unit));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == unit));
    }
    [Test] public void BattleBoundModifierExpiresAfterCombatButOtherModifiersRemain()
    {
        Setup(); provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Modify, Amount = 300 },
            new EffectInstruction { EffectId = "Schedule", Timing = "nextBattle", After = new[] { new EffectInstruction { Op = EffectOp.Modify, Amount = 700, Duration = "battle" } } } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.AreEqual(1300, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
    }
    [TestCase("nextMain", true)] [TestCase("nextOpponentMain", true)] [TestCase("nextOwnMain", false)]
    public void MainSchedulesRespectWhoseNextMainIsRequired(string timing, bool expected)
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { EffectId = "Schedule", Timing = timing,
            After = new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "due", Value = EffectValue.Bool(true) } } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.AreEqual(expected, match.ForPlayer(0).Variables.Any(v => v.Key == "due"));
    }
    [Test] public void AttackDeclaredListenerCanRedirectAcrossOriginalRange()
    {
        Setup(); var board = new BattleBoard(); board.Connect(0, 1); board.Connect(1, 2);
        match = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, new EffectTestScenario { Phase = TestTurnPhase.Combat, Cards = new[] {
            new EffectTestCard { Id = "contract", Owner = 0, Zone = TestCardZone.Board, Node = 0 },
            new EffectTestCard { Id = "contract", Owner = 1, Zone = TestCardZone.Board, Node = 1 },
            new EffectTestCard { Id = "normal", Owner = 1, Zone = TestCardZone.Board, Node = 2 },
            new EffectTestCard { Id = "normal", Owner = 0, Zone = TestCardZone.Deck, Count = 5 },
            new EffectTestCard { Id = "normal", Owner = 1, Zone = TestCardZone.Deck, Count = 5 }
        } }, provider, new[] { 0 });
        Guid unit = match.ForPlayer(0).Board.Single(c => c.DefinitionId == "normal").Id;
        provider.Listeners["normal"] = new[] { new EventSubscription { Id = "redirect", Event = "AttackDeclared", Side = "opponent" } };
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { EffectId = "RedirectAttack" } };
        Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1)); AcceptPendingTriggers();
        Assert.IsTrue(Act(TestCommandKind.PassResponse, player: 1)); Assert.IsTrue(Act(TestCommandKind.PassResponse, player: 0));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Id == unit));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == Contract(1)));
    }
    [TestCase(false)] [TestCase(true)]
    public void DestructionWindowIsOptionalAndSuspendsLaterInstructions(bool accept)
    {
        Setup(); var source = Contract();
        provider.Listeners["contract"] = new[] { new EventSubscription { Id = "save", Event = "DestructionPending", Subject = "self" } };
        provider.ListenerPlan = (_, __) => new[] { new EffectInstruction { EffectId = "PreventDestruction", Target = "event", From = "effect" } };
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Destroy },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "continued", Value = EffectValue.Bool(true) } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, source));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == source));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "continued"));
        if (accept) AcceptPendingTriggers(); else Assert.IsTrue(Act(TestCommandKind.OrderTrigger, Guid.Empty));
        Assert.AreEqual(accept, match.ForPlayer(0).Board.Any(c => c.Id == source));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "continued"));
    }
    [Test] public void ChangingPendingBattleDestructionReasonPublishesOnlyEffectDestruction()
    {
        Setup(); string observed = null;
        provider.Listeners["contract"] = new[] {
            new EventSubscription { Id = "replace", Event = "DestructionPending", Subject = "self" },
            new EventSubscription { Id = "observe", Event = "Destroyed", Subject = "self" }
        };
        provider.ListenerPlan = (c, id) => {
            if (id == "replace") return new[] { new EffectInstruction { EffectId = "ChangeDestructionReason", From = "effect" } };
            observed = c.TriggerEvent.Reason; return Array.Empty<EffectInstruction>();
        };
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1)); AcceptPendingTriggers();
        Assert.AreEqual("effect", observed);
    }
    [TestCase(false)] [TestCase(true)]
    public void MidEffectPaymentCommitsOnlyItsSuccessBranchAndResumesOnCancel(bool pay)
    {
        Setup(); int hand = match.ForPlayer(0).OwnHand.Length;
        provider.Plan = _ => new[] {
            new EffectInstruction { EffectId = "Pay", Amount = 2, Costs = new[] { new ActivationCost { Kind = ActivationCostKind.Discard, Minimum = 1, Maximum = 1 } },
                After = new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "success", Value = EffectValue.Bool(true) } } },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "after", Value = EffectValue.Bool(true) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, pay ? Hand() : Guid.Empty));
        if (pay) Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(pay ? 2 : 4, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(hand - (pay ? 1 : 0), match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(pay, match.ForPlayer(0).Variables.Any(v => v.Key == "success"));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "after"));
    }
    [Test] public void TokenTimeCostDecrementsTokenOnlyAfterConfirmation()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = c => c.Event == EffectEvent.Activated ? new[] { new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "token" } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        var token = match.ForPlayer(0).PublicPiles.Single(c => c.IsToken);
        provider.Definitions["contract"].ActivationCosts = new[] { new ActivationCost { Kind = ActivationCostKind.TokenTime, Zone = TestCardZone.OffField, Minimum = 2, Maximum = 2 } };
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, token.Id));
        Assert.AreEqual(2, match.ForPlayer(0).PublicPiles.Single(c => c.Id == token.Id).Time);
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(0, match.ForPlayer(0).PublicPiles.Single(c => c.Id == token.Id).Time);
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
    }
    [Test] public void NameBlockAndOwnSelectionProtectionAreIndependent()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Definitions["normal"].HasActivate = true;
        Guid unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "ProtectSelection", Side = "own" }, new EffectInstruction { EffectId = "BlockName" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Choose, Type = "契约时魔" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit)); Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void TargetedMarkCopyReusesAppliedModifierWithoutRepeatingOtherSteps()
    {
        Setup(); provider.Definitions["contract"].ForesightCount = 1;
        provider.Definitions["normal"].ForesightMark = provider.Definitions["decision"].ForesightMark = ForesightMark.Special;
        provider.ForesightPlan = _ => new[] { new EffectInstruction { Op = EffectOp.Choose, Side = "opponent" },
            new EffectInstruction { Op = EffectOp.Modify, Target = "selected", Amount = -100 }, new EffectInstruction { Op = EffectOp.Draw, Amount = 1 } };
        provider.Listeners["contract"] = new[] { new EventSubscription { Id = "copy", Event = "ForesightResolved", Side = "own" } };
        provider.ListenerPlan = (c, _) => {
            Assert.AreEqual(TestCardZone.Board, ((ForesightResolvedEvent)c.TriggerEvent).Targets.Single().Zone);
            return new[] { new EffectInstruction { Op = EffectOp.Choose, Side = "own" }, new EffectInstruction { EffectId = "CopyForesight", Target = "selected" } };
        };
        int hand = match.ForPlayer(0).OwnHand.Length;
        Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1)); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract(1))); AcceptPendingTriggers();
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.AreEqual(900, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Assert.AreEqual(hand + 1, match.ForPlayer(0).OwnHand.Length);
    }
    [Test] public void GrantedOncePerTurnAbilityUsesItsOwnLimit()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "GrantTrigger", Key = "CostChanged", Callback = "once", OncePerTurn = true },
            new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        provider.ScryPlan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "fired", Value = EffectValue.Bool(true) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); AcceptPendingTriggers();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "fired"));
    }
    [Test] public void GrantedCostCancellationPreservesLimitUntilPaymentCommits()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "GrantTrigger", Key = "CostChanged", Callback = "paid", OncePerTurn = true,
            Costs = new[] { new ActivationCost { Kind = ActivationCostKind.Time, Minimum = 1, Maximum = 1 } } },
            new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        provider.ScryPlan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "paid_grant", Value = EffectValue.Bool(true) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: -1));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "paid_grant"));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Cost, Amount = -1 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(1, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "paid_grant"));
        Assert.IsNull(match.ForPlayer(0).Choice);
    }

    [Test] public void ExileTurnFilterAndQueryAppendBuildAUnionWithoutOldCards()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false; int found = 0;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand }, new EffectInstruction { Op = EffectOp.Move, Target = "selected", Zone = TestCardZone.Exile },
            new EffectInstruction { EffectId = "QueryCards", Zone = TestCardZone.Exile, EnteredThisTurn = true, StoreAs = "eligible" },
            new EffectInstruction { EffectId = "QueryCards", Side = "opponent", StoreAs = "eligible", Append = true },
            new EffectInstruction { EffectId = "Continue", Callback = "inspect" } };
        provider.ScryPlan = c => { found = c.Sets["eligible"].Length; Assert.AreEqual(c.Turn, c.Sets["eligible"].Single(v => v.Zone == TestCardZone.Exile).ZoneEnteredTurn); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Hand())); Assert.AreEqual(2, found);
    }
}
