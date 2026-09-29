using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed partial class EffectKernelTests
{
    private sealed class Provider : ICardEffectProvider, IScryEffectProvider, ICardNameProvider, IContinuousEffectProvider, IAutomaticEffectProvider, Mishi.Battle.Events.IEventEffectProvider, IForesightEffectProvider
    {
        public readonly Dictionary<string, CardEffectRules> Definitions = new Dictionary<string, CardEffectRules>();
        public Func<EffectContext, IReadOnlyList<EffectInstruction>> Plan = _ => Array.Empty<EffectInstruction>();
        public Func<EffectContext, IReadOnlyList<EffectInstruction>> ScryPlan = _ => Array.Empty<EffectInstruction>();
        public Func<EffectContext, IReadOnlyList<ContinuousModifier>> ContinuousPlan = _ => Array.Empty<ContinuousModifier>();
        public Func<EffectContext, IReadOnlyList<AutomaticEffectPlan>> AutomaticPlan;
        public Func<EffectContext, IReadOnlyList<EffectInstruction>> ForesightPlan = _ => null;
        public IReadOnlyList<EffectInstruction> BuildForesight(EffectContext c) => ForesightPlan(c);
        public readonly Dictionary<string, Mishi.Battle.Events.EventSubscription[]> Listeners = new Dictionary<string, Mishi.Battle.Events.EventSubscription[]>();
        public Func<EffectContext, string, IReadOnlyList<EffectInstruction>> ListenerPlan = (_, __) => Array.Empty<EffectInstruction>();
        public IReadOnlyList<Mishi.Battle.Events.EventSubscription> Subscriptions(string id, string eventId) => Listeners.TryGetValue(id, out var list) ? list.Where(s => s.Event == eventId).ToArray() : Array.Empty<Mishi.Battle.Events.EventSubscription>();
        public IReadOnlyList<EffectInstruction> BuildTriggered(EffectContext c, string id) => ListenerPlan(c, id);
        public IReadOnlyList<ContinuousModifier> Continuous(EffectContext c) => ContinuousPlan(c);
        public IReadOnlyList<AutomaticEffectPlan> BuildAutomatic(EffectContext c) => AutomaticPlan != null ? AutomaticPlan(c) : new[] { new AutomaticEffectPlan { Steps = Plan(c) } };
        public IReadOnlyList<string> CardNames => Definitions.Values.Select(r => r.Name).ToArray();
        public IReadOnlyList<EffectInstruction> BuildScry(EffectContext c, string callback) => ScryPlan(c);
        public CardEffectRules Rules(string id) => Definitions[id];
        public bool CanPlay(EffectContext c) => true;
        public bool CanActivate(EffectContext c) => true;
        public IReadOnlyList<EffectInstruction> Build(EffectContext c) => Plan(c);
    }
    private NetworkTestMatch match;
    private Provider provider;
    private long sequence;
    private void Setup(bool response = false)
    {
        var board = new BattleBoard(); board.Connect(0, 1); board.Connect(0, 2);
        var deck = new[] { new NetworkTestMatch.Card(Guid.Empty, "contract", 0, -1, 1000, true) }
            .Concat(Enumerable.Range(0, 20).Select(i => new NetworkTestMatch.Card(Guid.Empty, i % 2 == 0 ? "normal" : "decision", 0, -1, 1000, false, TestCardZone.Deck, i % 2 != 0, 0))).ToArray();
        match = NetworkTestMatch.FromDecks(board, new[] { 0, 1, 2 }, 0, 1, new[] { deck, deck }, new MatchDrawRules(10, 0, Array.Empty<TestTurnPhase>(), 120, 5, response ? 20 : 0), new Random(2));
        provider = new Provider();
        provider.Definitions.Add("contract", new CardEffectRules { Name = "Contract", Type = "契约时魔", HasActivate = true, OncePerTurn = true });
        provider.Definitions.Add("normal", new CardEffectRules { Name = "Normal", Type = "通常时魔", Race = "天使" });
        provider.Definitions.Add("decision", new CardEffectRules { Name = "Decision", Type = "决策卡", HasPlay = true, OwnTurnOnly = true });
        provider.Definitions.Add("token", new CardEffectRules { Name = "Token", Type = "衍生物", IsToken = true, Time = 2, Power = 1000 });
        match.AttachEffects(provider, ClockKind.White, ClockKind.Black, new[] { 0, 1 }); match.DealOpeningHands(); sequence = 0;
        Phase(TestTurnPhase.Main);
    }
    private bool Act(TestCommandKind kind, Guid id = default, int node = -1, int? player = null) => match.TryCommand(player ?? match.ActivePlayer, match.MatchId, ++sequence, match.Revision, kind, id, node, out _);
    private void Phase(TestTurnPhase phase) { while (match.Phase < phase) Assert.IsTrue(Act(TestCommandKind.NextPhase)); }
    private Guid Contract(int owner = 0) => match.ForPlayer(0).Board.First(c => c.Owner == owner && c.IsContract).Id;
    private Guid Hand(bool decision = false, int owner = 0) => match.ForPlayer(owner).OwnHand.First(c => c.IsDecision == decision).Id;
    [Test] public void TypedVariablesResolveOperandsAndExposeFreshCallbackCopies()
    {
        Setup();
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Key = "bonus", Value = EffectValue.Integer(1000) },
            new EffectInstruction { Op = EffectOp.AddValue, Key = "bonus", Value = EffectValue.Integer(500) },
            new EffectInstruction { Op = EffectOp.Modify, AmountReference = new VariableReference { Key = "bonus" } },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "saved", ValueReference = new VariableReference { Key = "bonus" } },
            new EffectInstruction { Op = EffectOp.SetValue, Key = "flag", Value = EffectValue.Bool(false) },
            new EffectInstruction { Op = EffectOp.Scry, AmountReference = new VariableReference { Scope = VariableScope.Card, Key = "count" }, AfterCallback = "check" }
        };
        // Initialize card scope in an earlier activation.
        var plan = provider.Plan;
        provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "count", Value = EffectValue.Integer(1) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        provider.Plan = plan;
        provider.ScryPlan = c => {
            Assert.AreEqual(1500, c.Variables[VariableScope.Effect]["bonus"].RequireInteger());
            Assert.IsFalse(c.Variables[VariableScope.Effect]["flag"].Boolean);
            Assert.AreEqual(1500, c.Variables[VariableScope.Player]["saved"].RequireInteger());
            c.Variables[VariableScope.Player].Clear(); // Context dictionary is detached from state.
            return Array.Empty<EffectInstruction>();
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(-1, match.Winner);
        Assert.AreEqual(2500, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Assert.AreEqual(1500, match.ForPlayer(0).Variables.Single(v => v.Key == "saved").Value.RequireInteger());
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Scope == VariableScope.Effect));
    }
    [Test] public void ScopeLifetimeAndNetworkPrivacyRemainSeparate()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Effect, Key = "temporary", Value = EffectValue.String("secret") },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "marker", Value = EffectValue.Bool(true) },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Key = "counter", Value = EffectValue.Integer(2) },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Player, Side = "opponent", Key = "counter", Value = EffectValue.Integer(9) },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Turn, Key = "turnFlag", Value = EffectValue.Bool(true), Visibility = "public" },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Match, Key = "shared", Value = EffectValue.String("visible"), Visibility = "public" },
            new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Match, Key = "privateMatch", Value = EffectValue.String("hidden") }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(2, match.ForPlayer(0).Variables.Single(v => v.Key == "counter").Value.RequireInteger());
        Assert.AreEqual(9, match.ForPlayer(1).Variables.Single(v => v.Key == "counter").Value.RequireInteger());
        Assert.IsFalse(match.ForPlayer(1).Variables.Any(v => v.Key == "marker" || v.Key == "privateMatch" || v.Key == "temporary"));
        Assert.IsTrue(match.ForPlayer(1).Variables.Any(v => v.Key == "shared"));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.Scope == VariableScope.Turn));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "marker"));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "counter"));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.Key == "shared"));
        Setup(); Assert.IsEmpty(match.ForPlayer(0).Variables);
    }
    [Test] public void CardVariablesClearOnLeaveAndReentry()
    {
        Setup(); provider.Definitions["normal"].HasActivate = true;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.SetValue, Scope = VariableScope.Card, Key = "marker", Value = EffectValue.Integer(1) } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsTrue(match.ForPlayer(0).Variables.Any(v => v.CardId == unit));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.CardId == unit));
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.IsFalse(match.ForPlayer(0).Variables.Any(v => v.CardId == unit));
    }
    [TestCase(false)] [TestCase(true)] public void InvalidVariableOperandsCannotExecuteMutation(bool wrongType)
    {
        Setup();
        provider.Plan = _ => wrongType ? new[] {
            new EffectInstruction { Op = EffectOp.SetValue, Key = "bad", Value = EffectValue.Bool(true) },
            new EffectInstruction { Op = EffectOp.Modify, AmountReference = new VariableReference { Key = "bad" } }
        } : new[] { new EffectInstruction { Op = EffectOp.Modify, AmountReference = new VariableReference { Key = "missing" } } };
        Assert.DoesNotThrow(() => Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(2, match.Winner);
        Assert.AreEqual(1000, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
    }
    [Test] public void ScrySelectedCardStaysInHandWhileOnlyRemainderIsDiscarded()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 3, StoreAs = "looked", After = new[] {
            new EffectInstruction { Op = EffectOp.Choose, Target = "set", FromSet = "looked", StoreAs = "picked" },
            new EffectInstruction { Op = EffectOp.Move, Target = "set", FromSet = "picked", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Move, Target = "set", FromSet = "looked", Except = "picked", Zone = TestCardZone.Discard }
        } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var ids = match.ForPlayer(0).Choice.Candidates;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsEmpty(match.ForPlayer(1).Choice.ViewedCards);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, ids[1]));
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == ids[1]));
        CollectionAssert.AreEquivalent(new[] { ids[0], ids[2] }, match.ForPlayer(0).PublicPiles.Where(c => c.Zone == TestCardZone.Discard).Select(c => c.Id));
        Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void NamedSetsSurviveAnotherSelectionAndOptionalSkip()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 3, StoreAs = "looked", After = new[] {
            new EffectInstruction { Op = EffectOp.Choose, Target = "set", FromSet = "looked", StoreAs = "picked" },
            new EffectInstruction { Op = EffectOp.RememberCards, Target = "set", FromSet = "looked", Except = "picked", StoreAs = "others" },
            new EffectInstruction { Op = EffectOp.Choose, Target = "set", FromSet = "others", StoreAs = "skipped", Optional = true },
            new EffectInstruction { Op = EffectOp.Move, Target = "set", FromSet = "picked", Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.Move, Target = "set", FromSet = "others", Except = "skipped", Zone = TestCardZone.Discard }
        } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var ids = match.ForPlayer(0).Choice.Candidates;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, ids[0]));
        CollectionAssert.AreEquivalent(ids.Skip(1), match.ForPlayer(0).Choice.Candidates);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect));
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == ids[0]));
        Assert.AreEqual(2, match.ForPlayer(0).PublicPiles.Count(c => c.Zone == TestCardZone.Discard));
    }
    [TestCase(true)] [TestCase(false)] public void DeclarationLocksBeforeBottomScryAndComparesName(bool correct)
    {
        Setup(); provider.Definitions["normal"].Name = provider.Definitions["decision"].Name = "Same name";
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.DeclareCardName, StoreAs = "guess" },
            new EffectInstruction { Op = EffectOp.Scry, From = "bottom", Amount = 1, StoreAs = "bottom", AfterCallback = "resolveGuess" }
        };
        provider.ScryPlan = c => {
            Assert.AreEqual(1, c.Sets["bottom"].Length);
            return c.ScryCards[0].Name == c.Results["guess"]
                ? new[] { new EffectInstruction { Op = EffectOp.Move, Target = "scry", Zone = TestCardZone.Hand } }
                : Array.Empty<EffectInstruction>();
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var before = match.ForPlayer(0).Choice;
        Assert.IsEmpty(before.ViewedCards); Assert.IsEmpty(before.Candidates);
        Assert.AreEqual(1, before.NameOptions.Count(n => n == "Same name"));
        Assert.IsEmpty(match.ForPlayer(1).Choice.NameOptions);
        int index = Array.IndexOf(before.NameOptions, correct ? "Same name" : "Contract");
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsFalse(Act(TestCommandKind.DeclareCardName, node: index, player: 1));
        Assert.IsFalse(Act(TestCommandKind.DeclareCardName, node: before.NameOptions.Length));
        Assert.IsTrue(Act(TestCommandKind.DeclareCardName, node: index));
        var viewed = match.ForPlayer(0).Choice.ViewedCards.Single();
        Assert.IsFalse(Act(TestCommandKind.DeclareCardName, node: index));
        StringAssert.Contains(correct ? "Same name" : "Contract", match.ForPlayer(1).LastAction);
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(correct, match.ForPlayer(0).OwnHand.Any(c => c.Id == viewed.Id));
    }
    [Test] public void DeclarationTimeoutLocksFirstNameAndMemoryDoesNotLeakToNextActivation()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.DeclareCardName, StoreAs = "guess" },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 1, AfterCallback = "check" } };
        provider.ScryPlan = c => { Assert.AreEqual("Contract", c.Results["guess"]); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.AdvanceTime(100));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 1, AfterCallback = "check" } };
        provider.ScryPlan = c => { Assert.IsEmpty(c.Results); Assert.IsEmpty(c.Sets); return Array.Empty<EffectInstruction>(); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
    }
    [TestCase(false)] [TestCase(true)] public void ScryRespectsCountPrivacyAndPublicReveal(bool reveal)
    {
        Setup(); var count = match.ForPlayer(0).DeckCounts[1];
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Side = "opponent", Amount = 32, Reveal = reveal } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var own = match.ForPlayer(0).Choice; var other = match.ForPlayer(1).Choice;
        Assert.IsTrue(own.IsDeckView); Assert.AreEqual(count, own.ViewedCards.Length);
        Assert.IsTrue(own.ViewedCards.All(c => c.Owner == 1));
        Assert.IsEmpty(other.ViewedCards); Assert.IsEmpty(other.Candidates); Assert.IsEmpty(other.DeckPositions);
        Assert.AreEqual(count, match.ForPlayer(0).DeckCounts[1]);
        Assert.AreEqual(reveal ? count : 0, match.DrainPresentations().Count(p => p.Kind == CardPresentationKind.Reveal));
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 0, player: 1));
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, node: 2));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0)); Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void ScryChoiceOrdersTopAndBottomBeforeContinuing()
    {
        Setup();
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Scry, Amount = 3, After = new[] { new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "scry", Position = "choose" } } },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 1 },
            new EffectInstruction { Op = EffectOp.Draw, Amount = 1 },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 1, From = "bottom" }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var ids = match.ForPlayer(0).Choice.Candidates;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, Guid.NewGuid(), 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, ids[0], 2));
        Assert.IsFalse(Act(TestCommandKind.ResolveDeckView, ids[0], 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, ids[2], 1));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, ids[1], 1));
        Assert.AreEqual(ids[2], match.ForPlayer(0).Choice.Candidates.Single());
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == ids[2]));
        Assert.AreEqual(ids[0], match.ForPlayer(0).Choice.Candidates.Single());
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
    }
    [TestCase("top")] [TestCase("bottom")] public void FixedScryDestinationPreservesTopToBottomOrder(string destination)
    {
        Setup();
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Scry, Amount = 2, From = "bottom", After = new[] { new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "scry", Position = destination } } },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 2, From = destination }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var ids = match.ForPlayer(0).Choice.Candidates;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        CollectionAssert.AreEqual(destination == "bottom" ? ids.Reverse().ToArray() : ids, match.ForPlayer(0).Choice.Candidates);
    }
    [Test] public void ScryContinuationTargetsExactViewedCardsAndCanPauseForChoice()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 2, After = new[] {
            new EffectInstruction { Op = EffectOp.Move, Target = "scry", ScryIndex = 2, Zone = TestCardZone.Exile },
            new EffectInstruction { Op = EffectOp.Choose, Target = "scry", ScryIndex = 1 },
            new EffectInstruction { Op = EffectOp.Move, Target = "selected", Zone = TestCardZone.Hand }
        } } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var ids = match.ForPlayer(0).Choice.Candidates;
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(match.ForPlayer(0).PublicPiles.Any(c => c.Id == ids[1] && c.Zone == TestCardZone.Exile));
        Assert.AreEqual(ids[0], match.ForPlayer(0).Choice.ViewedCards.Single().Id);
        Assert.IsEmpty(match.ForPlayer(1).Choice.ViewedCards);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, ids[0]));
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == ids[0]));
    }
    [Test] public void ScryTimeoutFinishesAllRemainingChoicesAndKeepsCommittedBottom()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 3, After = new[] { new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "scry", Position = "choose" } } },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 1, From = "bottom" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var id = match.ForPlayer(0).Choice.Candidates[0];
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, id, 2));
        Assert.IsTrue(match.AdvanceTime(100));
        Assert.AreEqual(id, match.ForPlayer(0).Choice.Candidates.Single());
        Assert.IsTrue(match.AdvanceTime(100)); Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void ScryRejectsInvalidAmountsBeforePayment()
    {
        Setup(); provider.Definitions["contract"].ActivateCost = 1;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 0 } };
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(4, match.ForPlayer(0).CostPointers[0]);
        Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void ReturnToDeckWorksIndependentlyOfScry()
    {
        Setup(); var id = Hand(); int deckCount = match.ForPlayer(0).DeckCounts[0];
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand },
            new EffectInstruction { Op = EffectOp.ReturnToDeck, Target = "selected", Position = "bottom" },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 1, From = "bottom" }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, id));
        Assert.AreEqual(deckCount + 1, match.ForPlayer(0).DeckCounts[0]);
        Assert.IsFalse(match.ForPlayer(0).OwnHand.Any(c => c.Id == id));
        Assert.AreEqual(id, match.ForPlayer(0).Choice.Candidates.Single());
    }
    [Test] public void EmptyScrySkipsChoiceAndCallbackReceivesEmptyArray()
    {
        Setup(); bool called = false;
        provider.ScryPlan = c => { called = true; Assert.IsEmpty(c.ScryCards); return Array.Empty<EffectInstruction>(); };
        provider.Plan = _ => new[] {
            new EffectInstruction { Op = EffectOp.Scry, Amount = 32, After = new[] { new EffectInstruction { Op = EffectOp.Move, Target = "scry", Zone = TestCardZone.Hand } } },
            new EffectInstruction { Op = EffectOp.Scry, Amount = 3, AfterCallback = "afterScry" }
        };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.IsTrue(called); Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void ScryCallbackFaultStopsMatchWithoutEscapingCommandHandler()
    {
        Setup();
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Scry, Amount = 2, AfterCallback = "afterScry" } };
        provider.ScryPlan = c => { Assert.AreEqual(2, c.ScryCards.Length); throw new FormatException("bad continuation"); };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.DoesNotThrow(() => Act(TestCommandKind.ResolveDeckView, node: 0));
        Assert.AreEqual(2, match.Winner); Assert.IsNull(match.ForPlayer(0).Choice);
    }
    [Test] public void PresentationsFollowSuccessfulActionsAndAreConsumedOnce()
    {
        Setup(); match.DrainPresentations();
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract(1)));
        Assert.IsEmpty(match.DrainPresentations());
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var activation = match.DrainPresentations().Single();
        Assert.AreEqual(CardPresentationKind.Effect, activation.Kind);
        Assert.AreEqual(1, match.EffectUseCount(Contract(), EffectEvent.Activated, false));
        Assert.IsEmpty(match.DrainPresentations());
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsEmpty(match.DrainPresentations());
        var decision = Hand(true);
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, decision));
        var played = match.DrainPresentations().Single();
        Assert.AreEqual(CardPresentationKind.Decision, played.Kind);
        Assert.Greater(played.Sequence, activation.Sequence);
        Assert.AreEqual(1, match.DecisionsUsed);
        match.RevealForPresentation(Hand());
        var reveal = match.DrainPresentations().Single();
        Assert.AreEqual(CardPresentationKind.Reveal, reveal.Kind);
        Assert.Greater(reveal.Sequence, played.Sequence);
    }
    [Test] public void SummonChoiceIsPrivateValidatedAndModifierExpires()
    {
        Setup();
        provider.Plan = c => c.Event == EffectEvent.Summoned ? new[] { new EffectInstruction { Op = EffectOp.Choose }, new EffectInstruction { Op = EffectOp.Modify, Target = "selected", Amount = 1000 } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.AreEqual(0, match.ChoicePlayer); Assert.IsFalse(match.TimerRunning);
        Assert.IsEmpty(match.ForPlayer(1).Choice.Candidates);
        Assert.IsFalse(Act(TestCommandKind.ChooseEffect, Contract(1)));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffect, Contract(), player: 1));
        AcceptPendingTriggers();
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.AreEqual(2000, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Assert.AreEqual(1000, match.ForPlayer(0).Board.Single(c => c.Id == Contract()).Power);
    }
    [Test] public void InvalidEffectPlanDoesNotSpendCostOrStartPartialResolution()
    {
        Setup(); var id = Hand(true); int handCount = match.ForPlayer(0).OwnHand.Length;
        int cost = match.ForPlayer(0).CostPointers[0];
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Draw, Amount = 1 }, new EffectInstruction { EffectId = "UnregisteredEffect" } };
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, id));
        Assert.AreEqual(handCount, match.ForPlayer(0).OwnHand.Length);
        Assert.AreEqual(cost, match.ForPlayer(0).CostPointers[0]);
        Assert.IsTrue(match.ForPlayer(0).OwnHand.Any(c => c.Id == id));
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void EnemyTurnRestrictionPrecedesPayment()
    {
        Setup(true); Phase(TestTurnPhase.Combat); Assert.IsTrue(Act(TestCommandKind.Attack, Contract(), 1));
        int before = match.ForPlayer(1).CostPointers[1];
        Assert.IsFalse(Act(TestCommandKind.PlayDecision, Hand(true, 1), player: 1));
        Assert.AreEqual(before, match.ForPlayer(1).CostPointers[1]);
        provider.Definitions["decision"].OwnTurnOnly = false;
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true, 1), player: 1));
    }
    [Test] public void RuneAuraAndExileAreAuthoritative()
    {
        Setup(); provider.Definitions["decision"].AuraPower = 300; provider.Definitions["decision"].AuraRace = "天使";
        provider.Plan = c => c.Event == EffectEvent.Played ? new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.OffField } } : Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, Hand(), 2));
        Assert.IsTrue(Act(TestCommandKind.PlayDecision, Hand(true)));
        Assert.AreEqual(1000, match.ForPlayer(0).Board.Single(c => !c.IsContract).Power);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 1));
        Assert.AreEqual(1300, match.ForPlayer(0).Board.Single(c => !c.IsContract).Power);
        provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.Move, Target = "all", Zone = TestCardZone.Exile } };
        // Explicit choose + move, so source-zone filtering is independent of destination.
        provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.OffField }, new EffectInstruction { Op = EffectOp.Move, Target = "selected", Zone = TestCardZone.Exile } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        var rune = match.ForPlayer(0).Choice.Candidates.Single(); Assert.IsTrue(Act(TestCommandKind.ChooseEffect, rune));
        Assert.AreEqual(TestCardZone.Exile, match.ForPlayer(1).PublicPiles.Single().Zone);
        Assert.AreEqual(1000, match.ForPlayer(0).Board.Single(c => !c.IsContract).Power);
    }
    [Test] public void TokenSpawnAndChoiceTimeoutDoNotEndTurn()
    {
        Setup(); provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "token" }, new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.OffField }, new EffectInstruction { Op = EffectOp.Modify, Target = "selected", Stat = "time", Amount = 1, Duration = "permanent" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(match.AdvanceTime(30)); // Mandatory placement times out to the first shared area.
        Assert.IsTrue(match.AdvanceTime(30)); // Then resolve the separate token-card choice.
        var token = match.ForPlayer(0).PublicPiles.Single(); Assert.IsTrue(token.IsToken); Assert.AreEqual(3, token.Time); Assert.AreEqual(1, match.Turn);
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, Contract()));
    }
    [Test] public void SpawnWaitsForOwnerToChooseSharedAreaAndPersistsPosition()
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "token" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsEmpty(match.ForPlayer(0).PublicPiles);
        CollectionAssert.AreEqual(new[] { 0, 1 }, match.ForPlayer(0).Choice.ZoneCandidates);
        Assert.IsEmpty(match.ForPlayer(1).Choice.ZoneCandidates);
        Assert.IsFalse(match.TimerRunning);
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 1, player: 1));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 77));
        Assert.IsFalse(Act(TestCommandKind.ChooseEffect, Contract()));
        Assert.IsEmpty(match.ForPlayer(0).PublicPiles);
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 1));
        foreach (var player in new[] { 0, 1 })
        {
            var token = match.ForPlayer(player).PublicPiles.Single();
            Assert.AreEqual(1, token.NodeId); Assert.AreEqual(0, token.Owner);
            Assert.AreEqual(TestCardZone.OffField, token.Zone);
            Assert.IsNull(match.ForPlayer(player).Choice);
        }
        Assert.IsTrue(match.TimerRunning);
    }
    [Test] public void OpponentCannotPlaceInOccupiedSharedArea()
    {
        Setup(); provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "token" } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 1));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract(1)));
        CollectionAssert.AreEqual(new[] { 0 }, match.ForPlayer(1).Choice.ZoneCandidates);
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        var tokens = match.ForPlayer(0).PublicPiles;
        Assert.AreEqual(2, tokens.Length); CollectionAssert.AreEquivalent(new[] { 0, 1 }, tokens.Select(t => t.NodeId));
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, tokens.Select(t => t.Owner));
    }
    [Test] public void FriendlyStacksTapLowerCardsAndFullEnemyOccupancyDoesNotCreateToken()
    {
        Setup(); provider.Definitions["contract"].OncePerTurn = false;
        provider.Plan = _ => new[] { new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "token" } };
        foreach (int zone in new[] { 0, 0, 1 })
        {
            Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
            Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: zone));
        }
        var pile = match.ForPlayer(0).PublicPiles.Where(c => c.NodeId == 0).OrderBy(c => c.StackOrder).ToArray();
        Assert.AreEqual(2, pile.Length); Assert.IsTrue(pile[0].Tapped); Assert.IsFalse(pile[1].Tapped);
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract(1)));
        Assert.AreEqual(3, match.ForPlayer(1).PublicPiles.Length);
        Assert.IsNull(match.ForPlayer(1).Choice);
        Assert.AreEqual(-1, match.Winner);
    }
    [Test] public void RunePlacementUsesSameOccupancyRulesAsTokens()
    {
        Setup(); provider.Plan = c => c.Event == EffectEvent.Played
            ? new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.OffField } }
            : Array.Empty<EffectInstruction>();
        var first = Hand(true); Assert.IsTrue(Act(TestCommandKind.PlayDecision, first));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 1));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        var second = Hand(true, 1); Assert.IsTrue(Act(TestCommandKind.PlayDecision, second));
        CollectionAssert.AreEqual(new[] { 0 }, match.ForPlayer(1).Choice.ZoneCandidates);
        Assert.IsFalse(Act(TestCommandKind.ChooseEffectZone, node: 1));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        var runes = match.ForPlayer(0).PublicPiles;
        Assert.AreEqual(1, runes.Single(c => c.Id == first).NodeId);
        Assert.AreEqual(0, runes.Single(c => c.Id == second).NodeId);
    }
    [Test] public void InstanceLimitAllowsAnotherCopyAndResetsEachTurn()
    {
        Setup();
        var rule = provider.Definitions["normal"]; rule.HasActivate = true; rule.OncePerTurn = true;
        var first = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, first, 2));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, first));
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, first));
        // Moving the used instance out of play does not consume another instance's allowance.
        provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.Choose }, new EffectInstruction { Op = EffectOp.Move, Target = "selected", Zone = TestCardZone.Exile } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, first));
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        var second = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, second, 2));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, second));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, second));
    }
    [Test] public void NameLimitSharesAcrossVersionsButNotOpponents()
    {
        Setup();
        provider.Definitions["contract"].OncePerTurn = false;
        provider.Definitions["contract"].OncePerNamePerTurn = true;
        provider.Definitions["normal"].Name = "Contract";
        provider.Definitions["normal"].HasActivate = true;
        provider.Definitions["normal"].OncePerNamePerTurn = true;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsFalse(match.CanActivateCard(unit));
        Phase(TestTurnPhase.End); Assert.IsTrue(Act(TestCommandKind.EndTurn)); Phase(TestTurnPhase.Main);
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract(1)));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ReentryResetsInstanceLimitButPreservesNameLimit(bool byName)
    {
        Setup(); var rule = provider.Definitions["normal"];
        rule.HasActivate = true; rule.OncePerTurn = !byName; rule.OncePerNamePerTurn = byName;
        var unit = Hand(); Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, unit));
        Assert.IsFalse(Act(TestCommandKind.ActivateEffect, unit));
        provider.Plan = c => new[] { new EffectInstruction { Op = EffectOp.Choose }, new EffectInstruction { Op = EffectOp.Move, Target = "selected", Zone = TestCardZone.Hand } };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffect, unit));
        provider.Plan = _ => Array.Empty<EffectInstruction>();
        Assert.IsTrue(Act(TestCommandKind.Summon, unit, 2));
        Assert.AreEqual(!byName, Act(TestCommandKind.ActivateEffect, unit));
    }
    [Test] public void AnyCircleActivationRejectsOffField()
    {
        Setup(); var rule = provider.Definitions["decision"]; rule.HasActivate = true;
        provider.Plan = c => c.Event == EffectEvent.Played ? new[] { new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.OffField } } : Array.Empty<EffectInstruction>();
        var id = Hand(true); Assert.IsTrue(Act(TestCommandKind.PlayDecision, id));
        Assert.IsTrue(Act(TestCommandKind.ChooseEffectZone, node: 0));
        Assert.IsFalse(match.CanActivateCard(id)); Assert.IsFalse(Act(TestCommandKind.ActivateEffect, id));
        rule.ActivateZone = TestCardZone.OffField;
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, id));
    }
    [TestCase(false)] [TestCase(true)]
    public void EffectDamageAndHealingOnlyMoveCostWhenExplicit(bool moveCost)
    {
        Setup();
        provider.Definitions["contract"].OncePerTurn = false;
        var damage = new EffectInstruction { Op = EffectOp.Damage, Amount = 2 };
        if (moveCost) damage.MoveCost = true;
        provider.Plan = _ => new[] { damage };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        Assert.AreEqual(6, match.ForPlayer(0).DamagePointers[0]);
        Assert.AreEqual(moveCost ? 6 : 4, match.ForPlayer(0).CostPointers[0]);
        var heal = new EffectInstruction { Op = EffectOp.Heal, Amount = 1 };
        if (moveCost) heal.MoveCost = true;
        provider.Plan = _ => new[] { heal };
        Assert.IsTrue(Act(TestCommandKind.ActivateEffect, Contract()));
        foreach (int viewer in new[] { 0, 1 })
        {
            Assert.AreEqual(5, match.ForPlayer(viewer).DamagePointers[0]);
            Assert.AreEqual(moveCost ? 5 : 4, match.ForPlayer(viewer).CostPointers[0]);
        }
    }
    [Test] public void ClockTypesAndExplicitRecoveryBounds()
    {
        var clock = new PlayerClock(); clock.SetKind(ClockKind.White); Assert.AreEqual(ClockKind.White, clock.Kind);
        clock.Pay(3); clock.TakeDamage(1); Assert.AreEqual(2, clock.RemainingTime); Assert.AreEqual(5, clock.DamagePointer);
        clock.Heal(1); Assert.AreEqual(2, clock.RemainingTime); Assert.AreEqual(4, clock.DamagePointer);
        clock.AdjustCost(99); Assert.AreEqual(11, clock.RemainingTime); clock.AdjustCost(-99); Assert.AreEqual(12, clock.CostPointer);
        clock.Heal(99); Assert.AreEqual(1, clock.DamagePointer); Assert.IsFalse(clock.Lost);
    }
}
