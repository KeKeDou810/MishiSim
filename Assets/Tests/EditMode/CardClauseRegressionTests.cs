using System;
using System.IO;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using NUnit.Framework;

public sealed class CardClauseRegressionTests
{
    private static ICardEffectProvider Load()
    {
        Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        var files = Directory.GetFiles("Content/Cards", "*.lua");
        var definitions = Array.CreateInstance(Find("CardDefinition"), files.Length);
        for (int i = 0; i < files.Length; i++) definitions.SetValue(Find("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { File.ReadAllText(files[i]) }), i);
        return (ICardEffectProvider)Activator.CreateInstance(Find("LuaBattleEffects"), new object[] { definitions });
    }

    [TestCase("HZ01-002-SP")] [TestCase("HZ01-023-R")]
    public void ReturningInspectedBottomCardTriggersBottomDraw(string definition)
    {
        var provider = Load();
        var board = new BattleBoard(); board.Connect(0, 2); board.Connect(2, 1);
        board.SetPlayerNode(0, 0); board.SetPlayerNode(1, 1);
        var scenario = new EffectTestScenario { Phase = TestTurnPhase.Main, Cards = new[] {
            new EffectTestCard { Id = "HZ01-020-R", Owner = 0, Zone = TestCardZone.Board, Node = 0 },
            new EffectTestCard { Id = "PD01-001-USR", Owner = 1, Zone = TestCardZone.Board, Node = 1 },
            new EffectTestCard { Id = definition, Owner = 0, Zone = TestCardZone.Hand },
            new EffectTestCard { Id = "PD01-004-C", Owner = 0, Zone = TestCardZone.Deck, Count = 8 },
            new EffectTestCard { Id = "HZ01-003-SP", Owner = 0, Zone = TestCardZone.Deck },
            new EffectTestCard { Id = "PD01-004-C", Owner = 1, Zone = TestCardZone.Deck, Count = 8 }
        } };
        var match = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, scenario, provider, new[] { 0, 1 });
        long sequence = 0;
        void Act(TestCommandKind kind, Guid card = default, int node = -1)
        { Assert.IsTrue(match.TryCommand(0, match.MatchId, ++sequence, match.Revision, kind, card, node, out var error), error); }
        bool decision = provider.Rules(definition).Type == "决策卡";
        Act(decision ? TestCommandKind.PlayDecision : TestCommandKind.Summon, match.ForPlayer(0).OwnHand.Single().Id, 2);
        if (!decision) Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id);
        Assert.AreEqual("HZ01-003-SP", match.ForPlayer(0).Choice.ViewedCards.Single().DefinitionId);
        Act(TestCommandKind.ResolveDeckView, node: 0);
        Act(TestCommandKind.ChooseEffect); // Explicitly choose the return-to-bottom branch.
        Assert.IsNotEmpty(match.ForPlayer(0).Choice.TriggerOptions);
        int hand = match.ForPlayer(0).OwnHand.Length;
        Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id);
        Assert.AreEqual(hand + 1, match.ForPlayer(0).OwnHand.Length);
        Assert.IsNull(match.ForPlayer(0).Choice);
    }

    [TestCase("PD03-001-USR")] [TestCase("PD03-003-C")] [TestCase("PD03-005-C")]
    public void EggCreationRequiresExactBaseTokenButUpgradeCanTargetForms(string id)
    {
        var provider = Load();
        foreach (string name in new[] { "龙蛋衍生物", "龙蛋衍生物-飞龙形态" })
            foreach (string type in new[] { "衍生物", "通常时魔" })
            {
                var ctx = new EffectContext { DefinitionId = id, Reason = "battle", Event = id == "PD03-003-C" ? EffectEvent.Destroyed : EffectEvent.Summoned,
                    PublicCards = new[] { new ScryCardInfo { Owner = 0, Zone = TestCardZone.OffField, Name = name, Type = type } } };
                var plan = id == "PD03-001-USR" ? ((IScryEffectProvider)provider).BuildScry(ctx, "egg") : provider.Build(ctx);
                bool exists = name == "龙蛋衍生物" && type == "衍生物";
                Assert.AreEqual(exists ? "QueryCards" : "Spawn", plan[0].OperationId, id + ": " + name + "/" + type);
                if (exists) Assert.AreEqual("fuzzy", plan[0].NameMatch);
            }
    }

    [TestCase(false)] [TestCase(true)]
    public void XuanyuanPlayerRequiresEffectRebuildRatherThanPhaseRebuild(bool effectRebuild)
    {
        var provider = Load(); var rule = provider.Rules("HZ01-016-R");
        var attacker = new NetworkTestMatch.Card(Guid.NewGuid(), "HZ01-016-R", 0, 0, 1000, true);
        var ctx = new EffectContext { DefinitionId = "HZ01-041B-C", Owner = 0, Turn = 3, OpponentClock = ClockKind.White,
            TriggerEvent = new BattleEndedEvent(new EventCard(attacker, rule, true), null, 1, false),
            PublicCards = new[] { new ScryCardInfo { InstanceId = attacker.Id.ToString("N"), LastRebuiltTurn = 3, LastEffectRebuiltTurn = effectRebuild ? 3 : -1 } } };
        Assert.AreEqual(effectRebuild ? 1 : 0, ((IEventEffectProvider)provider).BuildTriggered(ctx, "contract_ready").Count);
    }

    [Test] public void EggGuardProtectsAgainstAllDestructionDuringTheBattle()
    {
        var provider = Load(); var rule = provider.Rules("PD03-001-USR");
        var target = new NetworkTestMatch.Card(Guid.NewGuid(), "PD03-001-USR", 0, 0, 1000, true);
        var ctx = new EffectContext { DefinitionId = "PD03-000B-C", Owner = 0, ContractName = rule.Name,
            TriggerEvent = new AttackDeclaredEvent(new EventCard(target, rule, true), new EventCard(target, rule, true), 0, 0) };
        var step = ((IEventEffectProvider)provider).BuildTriggered(ctx, "egg_guard").Single();
        Assert.AreEqual("any", step.From); Assert.AreEqual("battle", step.Duration);
    }
}

