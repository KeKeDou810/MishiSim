using System;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [TestCase("own", 0)] [TestCase("opponent", 1)]
    public void ShuffleChangesOnlyChosenDeckOrderWithoutLosingOrRevealingCards(string side, int player)
    {
        Setup(); var hand = match.ForPlayer(0).OwnHand.Select(c => c.Id).ToArray();
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Scry, Amount = 10 },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 10, Side = "opponent" },
            new EffectInstruction { EffectId = "Shuffle", Side = side },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 10 },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 10, Side = "opponent" }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Guid[] Read() => match.ForPlayer(0).Choice.ViewedCards.Select(c => c.Id).ToArray();
        var beforeOwn = Read(); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        var beforeOther = Read(); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        var afterOwn = Read(); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        var afterOther = Read(); Assert.IsEmpty(match.ForPlayer(1).Choice.ViewedCards);
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        CollectionAssert.AreEquivalent(beforeOwn, afterOwn); CollectionAssert.AreEquivalent(beforeOther, afterOther);
        CollectionAssert.AreEqual(player == 0 ? beforeOther : beforeOwn, player == 0 ? afterOther : afterOwn);
        Assert.IsFalse((player == 0 ? beforeOwn : beforeOther).SequenceEqual(player == 0 ? afterOwn : afterOther));
        CollectionAssert.AreEqual(hand, match.ForPlayer(0).OwnHand.Select(c => c.Id));
        Assert.AreEqual(4, match.ForPlayer(0).DamagePointers[player]); Assert.AreEqual(4, match.ForPlayer(0).CostPointers[player]);
        Assert.IsEmpty(match.ForPlayer(1).PublicPiles);
    }
    [Test] public void MultiCardDrawRecyclesImmediatelyAndContinuesWithSynchronizedDamageAndCost()
    {
        Setup(); var oldDiscard = match.ForPlayer(0).OwnHand.Select(c => c.Id).ToArray();
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Discard, Target = "all", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Draw, Amount = 12 }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var view = match.ForPlayer(0);
        Assert.AreEqual(12, view.OwnHand.Length); Assert.AreEqual(8, view.DeckCounts[0]);
        Assert.AreEqual(2, view.OwnHand.Count(c => oldDiscard.Contains(c.Id)));
        Assert.IsEmpty(view.PublicPiles); Assert.IsEmpty(match.ForPlayer(1).PublicPiles);
        Assert.AreEqual(6, view.DamagePointers[0]); Assert.AreEqual(6, view.CostPointers[0]);
    }
    [Test] public void EmptyDiscardDoesNotRepeatedlyDamageAndLateDiscardRebuildsSameEmptyDeck()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 20 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(6, match.ForPlayer(0).DamagePointers[0]);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(6, match.ForPlayer(0).DamagePointers[0]);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Discard, Target = "all", Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(20, match.ForPlayer(0).DeckCounts[0]); Assert.AreEqual(6, match.ForPlayer(0).CostPointers[0]);
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 20 } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(8, match.ForPlayer(0).DamagePointers[0]); Assert.AreEqual(8, match.ForPlayer(0).CostPointers[0]);
    }
    [Test] public void LethalDeckExhaustionStopsRemainingDrawsAndEffectSteps()
    {
        Setup(); match.ResolvePlayerDamage(0, 6);
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Discard, Target = "all", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Draw, Amount = 12 },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "shouldNotRun", Value = EffectValue.Bool(true) }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(1, match.Winner); Assert.AreEqual(10, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(10, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Key == "shouldNotRun"));
    }
    [Test] public void MovingAllDeckCardsByScryRecyclesBeforeTheNextInstruction()
    {
        Setup(); provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Discard, Target = "all", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 10, After = new[] {
                new EffectInstruction { Op = EffectOp.Move, Target = "scry", Zone = TestCardZone.Hand },
                new EffectInstruction { Op = EffectOp.Heal, Amount = 1 }
            } }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract())); Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(5, match.ForPlayer(0).DamagePointers[0]); Assert.AreEqual(6, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(10, match.ForPlayer(0).DeckCounts[0]);
    }
}
