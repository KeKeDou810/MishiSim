using System;
using System.IO;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

// Reflection keeps the pure battle test assembly independent of Assembly-CSharp/MoonSharp.
public sealed partial class RealCardWorkflowTests
{
    [TestCase("", true)]
    [TestCase("playTurn='own',", true)]
    [TestCase("playTurn='either',", false)]
    [TestCase("playTiming='ownTurn',", true)]
    [TestCase("playTiming='response',", false)]
    public void LuaDecisionTurnScopePreservesUsageRules(string options, bool ownOnly)
    {
        Assert.AreEqual(ownOnly, DecisionTurnProvider(options).Rules("timing").OwnTurnOnly);
    }
    [TestCase("playTurn='response',")]
    [TestCase("playTurn=false,")]
    [TestCase("playTiming='typo',")]
    [TestCase("playTurn='own',playTiming='ownTurn',")]
    public void LuaDecisionTurnScopeRejectsAmbiguousOrInvalidConfiguration(string options)
    {
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => DecisionTurnProvider(options));
        Assert.IsInstanceOf<FormatException>(error.InnerException);
    }
    private static ICardEffectProvider DecisionTurnProvider(string options)
    {
        string lua = "return {id='timing',name='Timing',type='决策卡',faction='test',level=0,power=0,sign='无',race='',artworkPath='x.png',effects={"
            + options + "onPlay=function(ctx) return {{op='Draw',amount=1}} end}}";
        var definition = TypeOf("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { lua });
        var definitions = Array.CreateInstance(TypeOf("CardDefinition"), 1);
        definitions.SetValue(definition, 0);
        return (ICardEffectProvider)Activator.CreateInstance(TypeOf("LuaBattleEffects"), new object[] { definitions });
    }
    [TestCase("Damage", "", false)]
    [TestCase("Heal", "", false)]
    [TestCase("Damage", ",moveCost=true", true)]
    [TestCase("Heal", ",moveCost=true", true)]
    public void LuaDamageAndHealingDefaultToIndependentCost(string op, string option, bool expected)
    {
        string lua = "return {id='clock',name='Clock',type='决策卡',faction='test',level=0,power=0,sign='无',race='',artworkPath='x.png',effects={onPlay=function(ctx) return {{op='"
            + op + "',amount=1" + option + "}} end}}";
        var definition = TypeOf("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { lua });
        var definitions = Array.CreateInstance(TypeOf("CardDefinition"), 1); definitions.SetValue(definition, 0);
        var effects = (ICardEffectProvider)Activator.CreateInstance(TypeOf("LuaBattleEffects"), new object[] { definitions });
        Assert.AreEqual(expected, effects.Build(new EffectContext { DefinitionId = "clock", Event = EffectEvent.Played })[0].MoveCost);
    }
    private NetworkTestMatch match;
    private ICardEffectProvider provider;
    private object database;
    private long sequence;
    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    [TestCase("01-summon-combo")] [TestCase("02-scry-summon")] [TestCase("03-rune-combo")]
    [TestCase("04-overclock-return")] [TestCase("05-carrier")] [TestCase("06-foresight")]
    [TestCase("07-dice-invocation")] [TestCase("08-token-transformation")]
    public void EffectLabPresetsLoadActualLuaWithoutInitialTriggers(string name)
    {
        Setup("PD02-004-C");
        var json = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", true);
        var scenario = (EffectTestScenario)json.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) }).Invoke(null,
            new object[] { File.ReadAllText("Content/Tests/" + name + ".json"), typeof(EffectTestScenario) });
        var board = new BattleBoard();
        for (int i = 0; i < 12; i++) board.Connect(i, i + 1);
        board.SetPlayerNode(4, 0); board.SetPlayerNode(12, 1);
        board.SetPlayerOrDefenseNode(4, 0); board.SetPlayerOrDefenseNode(12, 1);
        var lab = NetworkTestMatch.FromEffectTest(board, Enumerable.Range(0, 13), 4, 12, scenario, provider, new[] { 0, 1 });
        Assert.IsFalse(lab.OpeningPending); Assert.AreEqual(-1, lab.ChoicePlayer); Assert.AreEqual(-1, lab.Winner);
        Assert.AreEqual(scenario.Phase, lab.Phase); Assert.AreEqual(scenario.ActivePlayer, lab.ActivePlayer);
        var view = lab.ForPlayer(0);
        Assert.AreEqual(scenario.Cards.Where(c => c.Owner == 0 && c.Zone == TestCardZone.Hand).Sum(c => c.Count), view.OwnHand.Length);
        Assert.AreEqual(scenario.Cards.Where(c => c.Owner == 1 && c.Zone == TestCardZone.Hand).Sum(c => c.Count), view.OpponentHandCount);
        Assert.AreEqual(scenario.Cards.Where(c => c.Owner == 0 && c.Zone == TestCardZone.Deck).Sum(c => c.Count), view.DeckCounts[0]);
        if (name == "04-overclock-return")
        { Assert.IsTrue(view.Board.Single(c => c.Owner == 0 && c.IsContract).Covered); Assert.IsTrue(view.Board.Single(c => c.Owner == 0 && c.IsContract).Tapped); }
        if (name == "02-scry-summon")
        {
            var contract = view.Board.Single(c => c.Owner == 0 && c.IsContract);
            Assert.IsTrue(lab.TryCommand(0, lab.MatchId, 1, lab.Revision, TestCommandKind.ActivateEffect, contract.Id, -1, out _));
            Assert.IsTrue(lab.TryCommand(0, lab.MatchId, 2, lab.Revision, TestCommandKind.ChooseNumber, Guid.Empty, 2, out _));
            Assert.IsTrue(lab.TryCommand(0, lab.MatchId, 3, lab.Revision, TestCommandKind.ChooseNumber, Guid.Empty, 0, out _));
            Assert.AreEqual("PD02-004-C", lab.ForPlayer(0).Choice.ViewedCards.Single().DefinitionId);
        }
    }
    [TestCase(false)] [TestCase(true)] public void LuaParsesDecisionTriggerCostsAndContinuousRange(bool legacy)
    {
        string lua = @"return {
            id='paid', name='Paid', type='通常时魔', faction='test',level=0,power=0,sign='无',race='test',artworkPath='x.png',
            effects={
                playCosts={{kind='Discard',amount=1}},
                continuous=function(ctx) return {{id='reach',stat='range',amount=1}} end,
                triggers={{id='paid_draw', event='Summoned', listen={subject='self'}, costs={{kind='Time',min=1,max=3,storeAs='paid'}},
                    onTrigger=function(ctx) return {{op='Draw',amount={scope='effect',var='paid'}}} end}}
            }}";
        if (legacy) lua = lua.Replace("listen={subject='self'},", "");
        var definition = TypeOf("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { lua });
        var definitions = Array.CreateInstance(TypeOf("CardDefinition"), 1); definitions.SetValue(definition, 0);
        var effects = (ICardEffectProvider)Activator.CreateInstance(TypeOf("LuaBattleEffects"), new object[] { definitions });
        Assert.AreEqual(ActivationCostKind.Discard, effects.Rules("paid").PlayCosts.Single().Kind);
        Assert.AreEqual("range", ((IContinuousEffectProvider)effects).Continuous(new EffectContext { DefinitionId = "paid" }).Single().Stat);
        var costs = legacy ? ((IAutomaticEffectProvider)effects).BuildAutomatic(new EffectContext { DefinitionId = "paid", Event = EffectEvent.Summoned }).Single().Costs
            : ((Mishi.Battle.Events.IEventEffectProvider)effects).Subscriptions("paid", "Summoned").Single().Costs;
        Assert.AreEqual(3, costs.Single().Maximum);
        var plan = ((Mishi.Battle.Events.IEventEffectProvider)effects).BuildTriggered(new EffectContext { DefinitionId = "paid" }, "paid_draw");
        Assert.AreEqual("paid", plan.Single().AmountReference.Key);
    }
    [Test] public void LuaCanRequestStandaloneShuffleThroughRegisteredKernelHandler()
    {
        string lua = "return {id='shuffle',name='Shuffle',type='通常时魔',faction='test',level=0,power=0,sign='无',race='test',artworkPath='x.png',effects={onActivate=function(ctx) return {{op='Shuffle',side='opponent'}} end}}";
        var definition = TypeOf("LuaCardLoader").GetMethod("Load").Invoke(null, new object[] { lua });
        var definitions = Array.CreateInstance(TypeOf("CardDefinition"), 1); definitions.SetValue(definition, 0);
        var effects = (ICardEffectProvider)Activator.CreateInstance(TypeOf("LuaBattleEffects"), new object[] { definitions });
        var plan = effects.Build(new EffectContext { DefinitionId = "shuffle", Event = EffectEvent.Activated });
        Assert.AreEqual("Shuffle", plan.Single().OperationId); Assert.AreEqual("opponent", plan.Single().Side);
        Assert.Throws<FormatException>(() => Mishi.Battle.Effects.KernelEffectRegistry.CreateDefault().Validate(new EffectInstruction { EffectId = "Shuffle", Side = "any" }, effects));
    }
    [TestCase(2)] [TestCase(3)] public void RealDiceDecisionBranchesParseTypedStorageAndConditionalPayment(int result)
    {
        Setup("PD04-008-C");
        var context = new EffectContext { DefinitionId = "PD04-008-C" };
        context.Variables[VariableScope.Effect]["die"] = EffectValue.Integer(result);
        context.Sets["decision"] = new[] { new ScryCardInfo { DefinitionId = "PD01-014-C", Time = 3 } };
        var plan = ((IScryEffectProvider)provider).BuildScry(context, "afterDice");
        if (result == 2)
        {
            Assert.AreEqual("Pay", plan[0].OperationId); Assert.AreEqual(3, plan[0].Amount);
            Assert.AreEqual(ActivationCostKind.Discard, plan[0].Costs.Single().Kind);
            Assert.AreEqual("InvokeDecision", plan[0].After.Single().OperationId);
        }
        else { Assert.AreEqual("ReturnToDeck", plan[0].OperationId); Assert.AreEqual("Shuffle", plan[1].OperationId); }
    }
    [Test] public void RealDragonEggTransformationChoosesNodeAndCalculatesPower()
    {
        Setup("PD03-011-C");
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2); board.Connect(2, 3);
        var scenario = new EffectTestScenario { Cost = new[] { 11, 11 }, Cards = new[] {
            new EffectTestCard { Id = "PD01-001-USR", Owner = 0, Zone = TestCardZone.Board },
            new EffectTestCard { Id = "PD01-001-USR", Owner = 1, Zone = TestCardZone.Board },
            new EffectTestCard { Id = "PD03-011-C", Owner = 0, Zone = TestCardZone.Hand },
            new EffectTestCard { Id = "PD03-T01-C", Owner = 0, Zone = TestCardZone.OffField, Node = 0 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 0, Zone = TestCardZone.Deck, Count = 5 },
            new EffectTestCard { Id = "PD01-004-C", Owner = 1, Zone = TestCardZone.Deck, Count = 5 }
        } };
        var lab = NetworkTestMatch.FromEffectTest(board, new[] { 0, 1, 2, 3 }, 0, 1, scenario, provider, new[] { 0 });
        long seq = 0;
        bool ActLab(TestCommandKind kind, Guid id = default, int node = -1) => lab.TryCommand(0, lab.MatchId, ++seq, lab.Revision, kind, id, node, out _);
        var egg = lab.ForPlayer(0).PublicPiles.Single(c => c.IsToken);
        Assert.IsTrue(ActLab(TestCommandKind.Summon, lab.ForPlayer(0).OwnHand.Single().Id, 2));
        Assert.IsTrue(ActLab(TestCommandKind.OrderTrigger, lab.ForPlayer(0).Choice.TriggerOptions[0].Id));
        Assert.IsTrue(ActLab(TestCommandKind.ChooseEffect, egg.Id));
        Assert.IsTrue(lab.ForPlayer(0).Choice.IsBoardPlacement);
        Assert.IsTrue(ActLab(TestCommandKind.ChooseEffectZone, node: 3));
        var dragon = lab.ForPlayer(0).Board.Single(c => c.DefinitionId == "PD03-T02-C");
        Assert.AreEqual(egg.Time + 1, dragon.Time); Assert.AreEqual(Math.Min(13, dragon.Time) * 1000, dragon.Power);
        Assert.IsFalse(lab.ForPlayer(0).PublicPiles.Any(c => c.Id == egg.Id));
        Assert.AreEqual(-1, lab.Winner);
    }
    private void Setup(string unit, string contractId = "HZ01-024-R", ClockKind clock = ClockKind.White)
    {
        database = Activator.CreateInstance(TypeOf("CardDatabase"));
        database.GetType().GetMethod("LoadDirectory").Invoke(database, new object[] { Path.GetFullPath("Content/Cards") });
        provider = (ICardEffectProvider)Activator.CreateInstance(TypeOf("LuaBattleEffects"), database.GetType().GetProperty("All").GetValue(database));
        var normal = provider.Rules(unit);
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, contractId, 0, -1, 1000, true) }
            .Concat(Enumerable.Range(0, 49).Select(_ => new NetworkTestMatch.Card(Guid.Empty, unit, 0, -1, normal.Power, false, TestCardZone.Deck, normal.Type == "决策卡", normal.Time))).ToArray();
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2);
        match = NetworkTestMatch.FromDecks(board, new[] { 0, 1, 2, 3 }, 0, 1, new[] { deck, deck },
            new MatchDrawRules(5, 0, Array.Empty<TestTurnPhase>(), 120, 5, 20), new Random(5));
        match.AttachEffects(provider, clock, clock);
        var definition = database.GetType().GetMethod("Get").Invoke(database, new object[] { contractId });
        var playerIds = new[] { (string)definition.GetType().GetProperty("PlayerCardId1").GetValue(definition), (string)definition.GetType().GetProperty("PlayerCardId2").GetValue(definition) };
        foreach (int owner in new[] { 0, 1 }) match.AddPlayerCards(owner, owner,
            new NetworkTestMatch.Card(Guid.NewGuid(), playerIds[0], owner, owner), new NetworkTestMatch.Card(Guid.NewGuid(), playerIds[1], owner, owner));
        match.DealOpeningHands(); Phase(TestTurnPhase.Main);
    }
    private bool Act(TestCommandKind kind, Guid id = default, int node = -1, int? owner = null) => match.TryCommand(owner ?? match.ActivePlayer, match.MatchId, ++sequence, match.Revision, kind, id, node, out _);
    private Guid Contract() => match.ForPlayer(0).Board.Single(c => c.Owner == 0 && c.IsContract).Id;
    private void Phase(TestTurnPhase phase) { while (match.Phase < phase) Assert.IsTrue(Act(TestCommandKind.NextPhase)); }
    [TestCase("HZ01-020-R")] [TestCase("HZ01-020-SP")]
    public void ActualCarrierSkillPaysOneAddsRangeForTurnAndAttachesPlayers(string contract)
    {
        Setup("PD01-004-C", contract, ClockKind.Black); var carrier = Contract();
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, carrier)); Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(3, match.ForPlayer(0).CostPointers[0]);
        Assert.AreEqual(2, match.ForPlayer(0).Board.Single(c => c.Id == carrier).AttackRange);
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.AttachedTo == carrier));
        Assert.IsTrue(Act(TestCommandKind.MoveContract, carrier, 2));
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.NodeId == 2));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(1, match.ForPlayer(0).Board.Single(c => c.Id == carrier).AttackRange);
        Assert.IsTrue(match.PlayerCards.Where(c => c.Owner == 0).All(c => c.AttachedTo == carrier && c.NodeId == 2));
    }
    [TestCase("流程测试-登场回牌底")] [TestCase("流程测试-复活与未命中")]
    public void WorkflowDeckFilesPassExternalTestModeValidation(string name)
    {
        Setup("PD02-004-C");
        var document = TypeOf("DeckStorage").GetMethod("Read").Invoke(null, new object[] { "Content/Decks", name });
        string modeId = (string)document.GetType().GetField("ModeId").GetValue(document);
        var modes = (Array)TypeOf("GameMode").GetMethod("Load").Invoke(null, new object[] { "Content/Rules/game-modes.json" });
        var mode = modes.Cast<object>().Single(m => (string)m.GetType().GetProperty("Id").GetValue(m) == modeId);
        var deck = Activator.CreateInstance(TypeOf("DeckModel"), mode);
        var entries = (System.Collections.Generic.Dictionary<string, int>)document.GetType().GetField("Cards").GetValue(document);
        foreach (var pair in entries.OrderByDescending(p => provider.Rules(p.Key).Type == "契约时魔"))
        {
            var card = database.GetType().GetMethod("Get").Invoke(database, new object[] { pair.Key });
            for (int i = 0; i < pair.Value; i++) Assert.IsTrue((bool)deck.GetType().GetMethod("TryAdd").Invoke(deck, new object[] { card, null }));
        }
        Assert.IsTrue((bool)deck.GetType().GetMethod("ValidateComplete").Invoke(deck, new object[] { null }));
    }
    [Test] public void VictoriaPaysXRevealsSummonsAndReturnsToBottomAtEnd()
    {
        Setup("PD02-004-C"); int deck = match.ForPlayer(0).DeckCounts[0];
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(Act(TestCommandKind.ChooseNumber, node: 2, owner: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 2)); Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        Assert.AreEqual(2, match.ForPlayer(0).CostPointers[0]); Assert.IsTrue(match.ForPlayer(0).Choice.IsDeckView);
        var summoned = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 0, owner: 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0)); Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        Assert.IsTrue(match.ForPlayer(1).Board.Any(c => c.Id == summoned && !c.Tapped));
        Assert.AreEqual(deck - 1, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
        Phase(TestTurnPhase.End);
        Assert.IsFalse(match.ForPlayer(1).Board.Any(c => c.Id == summoned));
        Assert.AreEqual(deck, match.ForPlayer(0).DeckCounts[0]); Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void VictoriaMissPlacesCardOnBottomThenDrawsWithoutPlayingDecision()
    {
        Setup("PD02-014-C"); int deck = match.ForPlayer(0).DeckCounts[0], hand = match.ForPlayer(0).OwnHand.Length;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 1)); Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0));
        var shown = match.ForPlayer(0).Choice.ViewedCards.Single().Id;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(hand + 1, match.ForPlayer(0).OwnHand.Length); Assert.AreEqual(deck - 1, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsFalse(match.ForPlayer(0).OwnHand.Any(c => c.Id == shown));
        Assert.AreEqual(0, match.DecisionsUsed); Assert.AreEqual(2, match.ForPlayer(0).Board.Length);
    }
    [Test] public void ActualPlayerLuaRevivesContractAfterDestructionAndTimeRebuild()
    {
        Setup("PD02-014-C"); var contract = Contract();
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, match.ForPlayer(0).OwnHand.First().Id));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, contract));
        var player = match.PlayerCards.Single(c => c.Owner == 0 && c.DefinitionId == "HZ01-051A-C");
        Assert.IsFalse(player.Covered); Assert.IsFalse(Act(TestCommandKind.ActivateEffect, player.Id));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, player.Id));
        Assert.IsTrue(Act(TestCommandKind.ChooseNumber, node: 0)); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, contract));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 2));
        Assert.AreEqual(1, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(match.PlayerCards.Single(c => c.Id == player.Id).Covered);
        Assert.IsTrue(match.ForPlayer(1).Board.Any(c => c.Id == contract && c.NodeId == 2));
        Assert.AreEqual(-1, match.Winner);
    }
}
