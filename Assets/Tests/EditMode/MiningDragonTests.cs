using System;
using System.IO;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class MiningDragonTests
{
    [TestCase("PR-002-PR", true)] [TestCase("PR-002-PR", false)]
    [TestCase("HZ01-068-C", true)] [TestCase("HZ01-068-C", false)]
    public void SummonSelectsAtMostOneHighTimeUnitAndDiscardsRemainder(string id, bool take)
    {
        Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        var files = Directory.GetFiles("Content/Cards", "*.lua");
        var definitions = Array.CreateInstance(Find("CardDefinition"), files.Length);
        for (int i = 0; i < files.Length; i++)
            definitions.SetValue(Find("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { File.ReadAllText(files[i]) }), i);
        var provider = (ICardEffectProvider)Activator.CreateInstance(Find("LuaBattleEffects"), new object[] { definitions });
        var board = new BattleBoard(); board.Connect(0, 2); board.Connect(2, 1);
        board.SetPlayerNode(0, 0); board.SetPlayerNode(1, 1);
        var scenario = new EffectTestScenario { ActivePlayer = 0, Phase = TestTurnPhase.Main, Cards = new[] {
            new EffectTestCard { Id = "PD01-001-USR", Owner = 0, Zone = TestCardZone.Board, Node = 0 },
            new EffectTestCard { Id = "PD01-001-USR", Owner = 1, Zone = TestCardZone.Board, Node = 1 },
            new EffectTestCard { Id = id, Owner = 0, Zone = TestCardZone.Hand },
            new EffectTestCard { Id = "PD01-010-C", Owner = 0, Zone = TestCardZone.Deck, Count = 2 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 0, Zone = TestCardZone.Deck, Count = 6 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 1, Zone = TestCardZone.Deck, Count = 8 }
        } };
        var match = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2 }, 0, 1, scenario, provider, new[] { 0, 1 });
        long sequence = 0;
        void Act(TestCommandKind kind, Guid card = default, int node = -1)
        { Assert.IsTrue(match.TryCommand(0, match.MatchId, ++sequence, match.Revision, kind, card, node, out var error), error); }
        Act(TestCommandKind.Summon, match.ForPlayer(0).OwnHand.Single().Id, 2);
        Act(TestCommandKind.OrderTrigger, match.ForPlayer(0).Choice.TriggerOptions.Single().Id);
        Assert.AreEqual(5, match.ForPlayer(0).Choice.ViewedCards.Length);
        Assert.IsEmpty(match.ForPlayer(1).Choice.ViewedCards);
        Act(TestCommandKind.ResolveDeckView, node: 0);
        Assert.AreEqual(2, match.ForPlayer(0).Choice.Candidates.Length);
        Act(TestCommandKind.ChooseEffect, take ? match.ForPlayer(0).Choice.Candidates[0] : Guid.Empty);
        Assert.AreEqual(take ? 1 : 0, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(take ? 4 : 5, match.ForPlayer(0).PublicPiles.Count(c => c.Owner == 0 && c.Zone == TestCardZone.Discard));
        Assert.AreEqual(3, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsNull(match.ForPlayer(0).Choice);
        Assert.AreEqual(-1, match.Winner);
    }
}
