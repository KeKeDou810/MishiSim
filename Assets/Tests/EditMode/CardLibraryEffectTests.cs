using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed partial class RealCardWorkflowTests
{
    [Test] public void EveryEffectBearingRulesGroupHasAnExecutableEntryAndMatchingVariantScripts()
    {
        Setup("PD01-004-C");
        var variants = ((IEnumerable)database.GetType().GetProperty("All").GetValue(database)).Cast<object>().ToArray();
        string Field(object c, string name) => (string)c.GetType().GetProperty(name).GetValue(c);
        foreach (var group in variants.GroupBy(c => Field(c, "RulesId")))
        {
            var first = group.First(); string text = Field(first, "EffectText");
            if (string.IsNullOrWhiteSpace(text)) continue;
            string body = Field(first, "ScriptSource").Split(new[] { "effects =" }, StringSplitOptions.None).Last();
            Assert.IsTrue(Regex.IsMatch(body, @"\b(onPlay|onActivate|onSummon|onDestroyed|onForesight|continuous|triggers)\s*="), group.Key);
            foreach (var variant in group)
                Assert.AreEqual(Regex.Replace(body, @"\s+", ""), Regex.Replace(Field(variant, "ScriptSource").Split(new[] { "effects =" }, StringSplitOptions.None).Last(), @"\s+", ""), Field(variant, "Id"));
        }
    }

    [Test] public void ActualLibraryCallbacksParseTheirInstructionsForBothClocksAndBranchResults()
    {
        Setup("PD01-004-C");
        var variants = ((IEnumerable)database.GetType().GetProperty("All").GetValue(database)).Cast<object>();
        foreach (var card in variants)
        {
            string id = (string)card.GetType().GetProperty("Id").GetValue(card);
            string script = (string)card.GetType().GetProperty("ScriptSource").GetValue(card);
            var callbacks = Regex.Matches(script, @"(?m)^        (\w+)\s*=\s*function\(ctx\)").Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value).Where(n => n != "canActivate" && n != "canPlay" && n != "continuous").Distinct();
            foreach (int branch in new[] { 0, 1, 2 })
            {
                var rule = provider.Rules(id);
                var source = new NetworkTestMatch.Card(Guid.NewGuid(), id, 0, 2, rule.Power, rule.Type == "契约时魔", TestCardZone.Board, false, rule.Time);
                var c = new EffectContext { DefinitionId = id, InstanceId = source.Id.ToString("N"), Owner = 0, ActivePlayer = 0, Turn = 3, Node = 2,
                    Clock = branch == 0 ? ClockKind.Black : ClockKind.White, OpponentClock = branch == 1 ? ClockKind.Black : ClockKind.White,
                    SourceZone = TestCardZone.Board, ContractName = "轩辕 超星机甲 维克多莉娅 乌列尔 异兽 艾莲 莉莉 赤羽 优尔 梵祢莉娅",
                    Cost = 8, Power = 12000, Time = 2, HandCount = 5, EmptyBoardCount = 3, Reason = branch == 0 ? "battle" : "paidHand",
                    TriggerEvent = new BattleEndedEvent(new EventCard(source, rule, true), null, 1, false), OwnFieldNameCounts = new Dictionary<string, int>() };
                foreach (string name in new[] { "paidTime", "discarded", "die", "bonus", "amount" }) c.Variables[VariableScope.Effect][name] = EffectValue.Integer(branch + 1);
                c.Variables[VariableScope.Effect]["guess"] = EffectValue.String(branch == 0 ? "测试卡" : "未命中");
                var looked = new ScryCardInfo { InstanceId = Guid.NewGuid().ToString("N"), DefinitionId = "PD01-004-C", Name = "测试卡", Type = branch == 0 ? "决策卡" : "通常时魔", Owner = 0, Time = branch * 3, Zone = TestCardZone.Deck };
                c.ScryCards = new[] { looked }; c.PublicCards = new[] { looked };
                foreach (string key in new[] { "picked", "viewed", "decision", "ally", "enemy", "egg", "eggs", "discarded", "summoned", "returning" }) c.Sets[key] = branch == 2 ? Array.Empty<ScryCardInfo>() : new[] { looked };
                foreach (string callback in callbacks)
                    Assert.DoesNotThrow(() => ((IScryEffectProvider)provider).BuildScry(c, callback), id + " / " + callback + " / branch " + branch);
                Assert.DoesNotThrow(() => ((IContinuousEffectProvider)provider).Continuous(c), id + " continuous");
                var subject = new EventCard(source, rule, true);
                var events = new BattleEvent[] {
                    new SummonedEvent(subject, TestCardZone.Hand, TestCardZone.Board, c.Reason, subject),
                    new DestroyedEvent(subject, TestCardZone.Board, TestCardZone.Discard, c.Reason, subject),
                    new DiscardedEvent(subject, TestCardZone.Hand, TestCardZone.Discard, "payment", subject),
                    new DeckPositionedEvent(subject, TestCardZone.Board, "bottom", subject),
                    new CardMovedEvent(subject, TestCardZone.Deck, TestCardZone.Hand, "effect", subject, "bottom"),
                    new DestructionPendingEvent(Guid.NewGuid(), subject, c.Reason, subject),
                    new ForesightRevealedEvent(subject, subject),
                    new AttackDeclaredEvent(subject, subject, 1, 2),
                    new PlayedEvent(subject, TestCardZone.Hand, TestCardZone.Discard, "play", subject),
                    new ForesightResolvedEvent(subject, subject, new[] { subject }, Array.Empty<EffectInstruction>(), Array.Empty<EffectInstruction>()),
                    c.TriggerEvent
                };
                var listeners = (IEventEffectProvider)provider;
                foreach (var notice in events)
                {
                    c.TriggerEvent = notice;
                    foreach (var subscription in listeners.Subscriptions(id, notice.Id))
                        Assert.DoesNotThrow(() => listeners.BuildTriggered(c, subscription.Id), id + " trigger " + subscription.Id + " / branch " + branch);
                }
            }
        }
    }

    [Test] public void NinaScalesFromCurrentHandAndWithdrawsOnOpponentsTurn()
    {
        Setup("PD01-011-C");
        var continuous = (IContinuousEffectProvider)provider;
        var c = new EffectContext { DefinitionId = "PD01-011-C", Owner = 0, ActivePlayer = 0, HandCount = 5 };
        Assert.AreEqual(2500, continuous.Continuous(c).Single().Amount);
        c.HandCount = 20; Assert.AreEqual(6000, continuous.Continuous(c).Single().Amount);
        c.ActivePlayer = 1; Assert.IsEmpty(continuous.Continuous(c));
    }

    private void LibraryScene(params EffectTestCard[] additional)
    {
        Setup("PD01-004-C"); sequence = 0;
        var board = new BattleBoard(); board.Connect(0, 2); board.Connect(2, 3); board.Connect(3, 1);
        board.SetPlayerNode(0, 0); board.SetPlayerNode(1, 1);
        board.SetPlayerOrDefenseNode(0, 0); board.SetPlayerOrDefenseNode(1, 1);
        var initial = new[] {
            new EffectTestCard { Id = "HZ01-024-R", Owner = 0, Zone = TestCardZone.Board, Node = 0 },
            new EffectTestCard { Id = "PD01-001-USR", Owner = 1, Zone = TestCardZone.Board, Node = 1 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 0, Zone = TestCardZone.Deck, Count = 8 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 1, Zone = TestCardZone.Deck, Count = 8 }
        };
        match = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2, 3 }, 0, 1, new EffectTestScenario {
            Phase = TestTurnPhase.Main, ActivePlayer = 0, Cards = initial.Concat(additional).ToArray()
        }, provider, new[] { 0, 1 });
    }

    [Test] public void RabiAdjustsPublicDeckCardsBeforeVictoriaChecksSummonTime()
    {
        LibraryScene(new EffectTestCard { Id = "HZ01-011-SR", Owner = 0, Zone = TestCardZone.Hand });
        Assert.IsTrue(Act(TestCommandKind.Summon, match.ForPlayer(0).OwnHand.Single().Id, 2));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        var shown = match.ForPlayer(0).Choice.ViewedCards.Single();
        Assert.AreEqual(provider.Rules(shown.DefinitionId).Time - 1, shown.Time);
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(match.ForPlayer(0).Choice.IsBoardPlacement);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 3));
        Assert.AreEqual(-1, match.Winner);
    }

    [Test] public void BottomLookCanAddChloeAndOfferHerSummonTrigger()
    {
        LibraryScene(new EffectTestCard { Id = "HZ01-008-SR", Owner = 0, Zone = TestCardZone.Deck },
            new EffectTestCard { Id = "HZ01-002-SP", Owner = 0, Zone = TestCardZone.Hand });
        Assert.IsTrue(Act(TestCommandKind.Summon, match.ForPlayer(0).OwnHand.Single().Id, 2));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id));
        Assert.AreEqual("HZ01-008-SR", match.ForPlayer(0).Choice.ViewedCards.Single().DefinitionId);
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        var chloe = match.ForPlayer(0).Choice.Candidates.Single();
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, chloe));
        Assert.IsTrue(Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 3));
        Assert.IsTrue(match.ForPlayer(0).Board.Any(c => c.Id == chloe && c.NodeId == 3));
        Assert.AreEqual(-1, match.Winner);
    }

    [Test] public void SelfBottomCostCannotSelectAnotherUnit()
    {
        LibraryScene(new EffectTestCard { Id = "HZ01-003-SP", Owner = 0, Zone = TestCardZone.Hand },
            new EffectTestCard { Id = "HZ01-043-C", Owner = 0, Zone = TestCardZone.Board, Node = 2 });
        // The actual self-bottom cost must not allow selecting a different unit.
        var source = match.ForPlayer(0).Board.Single(c => c.DefinitionId == "HZ01-043-C");
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, source.Id));
        Assert.AreEqual(new[] { source.Id }, match.ForPlayer(0).Choice.Candidates);
    }
}
