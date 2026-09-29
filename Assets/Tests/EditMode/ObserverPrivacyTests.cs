using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    [Test] public void ObserverHasBothHandCountsButNoHandIdentityOrVariables()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player,
            Key = "secret", Value = EffectValue.String("not-public") } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var observer = match.ForObserver();
        Assert.IsEmpty(observer.OwnHand); Assert.IsEmpty(observer.Variables);
        Assert.AreEqual(match.ForPlayer(0).OwnHand.Length, observer.HandCounts[0]);
        Assert.AreEqual(match.ForPlayer(1).OwnHand.Length, observer.HandCounts[1]);
        CollectionAssert.AreEqual(match.ForPlayer(0).DeckCounts, observer.DeckCounts);
        CollectionAssert.AreEquivalent(match.ForPlayer(0).Board.Select(c => c.Id), observer.Board.Select(c => c.Id));
    }
    [TestCase(false)] [TestCase(true)] public void ObserverLogsAndPresentationsRespectScryVisibility(bool reveal)
    {
        Setup(); match.DrainPresentations();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 3, Reveal = reveal } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var view = match.ForObserver();
        Assert.IsEmpty(view.Choice.Candidates); Assert.IsEmpty(view.Choice.NameOptions);
        Assert.IsEmpty(view.Choice.DeckPositions); Assert.IsEmpty(view.Choice.ZoneCandidates);
        Assert.AreEqual(reveal ? 3 : 0, match.DrainPresentations().Count(p => p.Audience < 0 && p.Kind == CardPresentationKind.Reveal));
        Assert.AreEqual(reveal ? 3 : 0, match.ActionLogFor(-1).Count(line => line.Contains("查看卡片：")));
        if (!reveal) Assert.IsEmpty(view.Choice.ViewedCards);
        Assert.IsFalse(view.LastAction.Contains("查看卡片：") && !reveal);
    }
    [Test] public void ObserverCannotReadOrSubmitNameDeclarationOptions()
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.DeclareCardName, StoreAs = "guess" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsNotEmpty(match.ForPlayer(0).Choice.NameOptions);
        Assert.IsEmpty(match.ForObserver().Choice.NameOptions);
        int revision = match.Revision;
        Assert.IsFalse(match.TryCommand(-1, match.MatchId, 1, revision, TestCommandKind.DeclareCardName, Guid.Empty, 0, out _));
        Assert.AreEqual(revision, match.Revision);
    }
    [Test] public void ObserverCannotSeeForeignFaceDownAttachments()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { EffectId = "ChooseHiddenHand", StoreAs = "stolen" },
            new EffectInstruction { EffectId = "AttachUnder", FromSet = "stolen" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, match.ForPlayer(0).Choice.Candidates[0]));
        var hidden = match.ForObserver().Board.Single(c => c.HiddenAttachment);
        Assert.AreEqual("", hidden.DefinitionId);
        Assert.AreEqual(0, hidden.Power);
    }
}
