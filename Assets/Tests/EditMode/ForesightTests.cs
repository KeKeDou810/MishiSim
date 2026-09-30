using System;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    private void ForesightSetup(int marks = 1, ForesightMark mark = ForesightMark.None, bool response = false)
    {
        Setup(response);
        provider.Definitions["contract"].ForesightCount = marks;
        provider.Definitions["normal"].ForesightMark = provider.Definitions["decision"].ForesightMark = mark;
        Phase(TestTurnPhase.Combat);
    }
    private void StartForesightAttack() => Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
    [TestCase(false)] [TestCase(true)]
    public void RevealedMarkCanBeDeclinedWithoutSkippingNextForesight(bool timeout)
    {
        ForesightSetup(2, ForesightMark.Heal);
        match.ResolvePlayerDamage(0, 1, moveCost: true);
        int revealedEvents = 0, resolvedEvents = 0;
        Listen("contract", new EventSubscription { Id = "reveal", Event = "ForesightRevealed", Subject = "self" },
            new EventSubscription { Id = "mark", Event = "ForesightResolved" });
        provider.ListenerPlan = (c, key) => { if (key == "reveal") revealedEvents++; else resolvedEvents++; return Array.Empty<EffectInstruction>(); };
        StartForesightAttack(); Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var revealed = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.Contains(3, match.ForPlayer(0).Choice.DeckPositions);
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 3, player: 1));
        if (timeout) Assert.IsTrue(match.AdvanceTime(21));
        else Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 3));
        Assert.AreEqual(5, match.ForPlayer(0).DamagePointers[0]);
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == revealed && c.Zone == TestCardZone.Discard));
        Assert.IsTrue(match.ForPlayer(0).Choice.IsForesightOffer);
        Assert.AreEqual(1, revealedEvents); Assert.AreEqual(0, resolvedEvents);
    }
    [Test] public void EachForesightCanTriggerDespiteUnrelatedCardActivationLimit()
    {
        ForesightSetup(2); int count = 0;
        Listen("contract", new EventSubscription { Id = "each", Event = "ForesightRevealed", Subject = "self", OncePerTurn = false, OncePerNamePerTurn = false });
        provider.ListenerPlan = (_, __) => { count++; return Array.Empty<EffectInstruction>(); };
        StartForesightAttack();
        for (int i = 0; i < 2; i++)
        {
            Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
            Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        }
        Assert.AreEqual(2, count);
    }
    [Test] public void ForesightReadyRemainsUprightAfterCombat()
    {
        ForesightSetup(1, ForesightMark.Special);
        provider.ForesightPlan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Choose, Side = "own" },
            new EffectInstruction { Op = EffectOp.Ready, Target = "selected" }
        };
        var attacker = Contract(); StartForesightAttack();
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, attacker));
        Assert.IsFalse(match.ForPlayer(0).Board.Single(c => c.Id == attacker).Tapped);
    }
    [TestCase(1, 2, 2)] [TestCase(2, 1, 2)] [TestCase(2, 3, 6)] [TestCase(0, 2, 0)]
    public void ForesightQaBudgetIsMarksTimesTargets(int marks, int targets, int expected)
    { Assert.AreEqual(expected, ForesightRules.MaximumChecks(marks, targets)); }
    [Test] public void ForesightTwoResolvesSequentiallyWithPublicScryAndDiscard()
    {
        ForesightSetup(2); int deck = match.ForPlayer(0).DeckCounts[0]; StartForesightAttack();
        Assert.IsTrue(match.ForPlayer(0).Choice.IsForesightOffer);
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var first = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.AreEqual(first, match.ForPlayer(1).Choice.ViewedCards.Single().Id);
        Assert.IsEmpty(match.ForPlayer(1).Choice.Candidates);
        Assert.AreEqual(deck - 1, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 0, player: 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(match.ForPlayer(0).Choice.IsForesightOffer);
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == first && c.Zone == TestCardZone.Discard));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Owner == 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var second = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.AreNotEqual(first, second);
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual(deck - 2, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == second && c.Zone == TestCardZone.Discard));
        Assert.IsFalse(match.ForPlayer(0).Board.Any(c => c.Owner == 1));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void ForesightCanSkipRemainingAndTimeoutDoesNotReveal()
    {
        ForesightSetup(2); int deck = match.ForPlayer(0).DeckCounts[0]; StartForesightAttack();
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1)); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 0));
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.AreEqual(deck - 1, match.ForPlayer(0).DeckCounts[0]);
        ForesightSetup(2); deck = match.ForPlayer(0).DeckCounts[0]; StartForesightAttack();
        Assert.IsTrue(match.AdvanceTime(21)); Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual(deck, match.ForPlayer(0).DeckCounts[0]);
    }
    [Test] public void ForesightRunsOnlyAfterBothPlayersPassResponse()
    {
        ForesightSetup(1, response: true); StartForesightAttack();
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.IsTrue(Act(TestCommandKind.PassResponse, player: 1)); Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.IsTrue(Act(TestCommandKind.PassResponse)); Assert.IsTrue(match.ForPlayer(0).Choice.IsForesightOffer);
    }
    [Test] public void DrawMarkCannotDrawTheRevealedCardAndDoesNotUseOnPlay()
    {
        ForesightSetup(1, ForesightMark.Draw); int hand = match.ForPlayer(0).OwnHand.Length;
        int deck = match.ForPlayer(0).DeckCounts[0]; StartForesightAttack();
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var revealed = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(hand + 1, match.ForPlayer(0).OwnHand.Length);
        Assert.IsFalse(match.ForPlayer(0).OwnHand.Any(c => c.Id == revealed));
        Assert.AreEqual(deck - 2, match.ForPlayer(0).DeckCounts[0]);
        Assert.AreEqual(0, match.DecisionsUsed); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void HealingMarkMovesToExileAndOnlyReducesDamage()
    {
        ForesightSetup(1, ForesightMark.Heal); match.ResolvePlayerDamage(0, 1, moveCost: true); StartForesightAttack();
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var revealed = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[0]); Assert.AreEqual(5, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == revealed && c.Zone == TestCardZone.Exile));
    }
    [Test] public void DestroyMarkCanCancelCombatAndStillDiscardsItsReveal()
    {
        ForesightSetup(2, ForesightMark.Destroy); var attacker = Contract(); StartForesightAttack();
        Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1)); var revealed = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, attacker));
        Assert.IsNull(match.ForPlayer(0).Choice); Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Owner == 1));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == revealed && c.Zone == TestCardZone.Discard));
    }
    [Test] public void SpecialMarkUsesOwnCallbackAndRetainsCardMovedOffField()
    {
        ForesightSetup(1, ForesightMark.Special);
        provider.ForesightPlan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.OffField } };
        StartForesightAttack(); Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1));
        var revealed = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0)); Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == revealed && c.Zone == TestCardZone.OffField));
        Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void ForesightPublishesAnEventBeforeCombatFinishes()
    {
        ForesightSetup(); bool seen = false;
        Listen("contract", new EventSubscription { Id = "reveal", Event = "ForesightRevealed", Subject = "self" });
        provider.ListenerPlan = (c, _) => { seen = ((ForesightRevealedEvent)c.TriggerEvent).Revealed != null;
            return new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "seen", Value = EffectValue.Bool(true) } }; };
        StartForesightAttack(); Assert.IsTrue(Act(TestCommandKind.ChooseForesight, node: 1)); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        AcceptPendingTriggers();
        Assert.IsTrue(seen); Assert.IsTrue(match.ForPlayer(0).Variables.Single(v => v.Key == "seen").Value.Boolean);
    }
}
