using System;
using Mishi.Battle.Effects;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        public int ChoicePlayer => effectChoice?.Player ?? -1;
        public double ChoiceSeconds => effectChoice?.Seconds ?? 0;
        private ICardEffectProvider effects;
        private readonly KernelEffectRegistry effectRegistry = KernelEffectRegistry.CreateDefault();
        private readonly Queue<EffectExecution> executions = new Queue<EffectExecution>();
        private readonly HashSet<string> activationCounts = new HashSet<string>(StringComparer.Ordinal);
        private EffectChoice effectChoice;
        private int[] sharedOffFieldZones = { 0 };
        private bool drainingEffects;
        private int effectSteps;
        private sealed class EffectExecution
        {
            public Card Source;
            public ForesightCombat Foresight;
            public IReadOnlyList<EffectInstruction> ResolvedMark;
            public readonly Dictionary<Guid, Events.EventCard> Affected = new Dictionary<Guid, Events.EventCard>();
            public readonly List<EffectInstruction> AppliedMarkSteps = new List<EffectInstruction>();
            public IReadOnlyList<EffectInstruction> Steps;
            public int Index;
            public EffectEvent? AutomaticEvent;
            public Events.BattleEvent EventData;
            public int SourceGeneration;
            public bool? OncePerTurn, OncePerNamePerTurn;
            public string AutomaticKey;
            public bool Started;
            public ActivationCost[] Costs = Array.Empty<ActivationCost>();
            public bool LockSourceGeneration;
            public Guid Selected;
            public readonly EffectVariableStore Variables = new EffectVariableStore();
            public string PendingChoiceStore;
            public SelectionState Selection;
            public readonly Dictionary<string, Guid[]> Sets = new Dictionary<string, Guid[]>(StringComparer.Ordinal);
            public Card[] ScryTargets = Array.Empty<Card>();
            public ScryState Scry;
            public DeckReturnState DeckReturn;
            public EffectInstruction PendingSpawn;
            public Queue<Card> PendingMoves = new Queue<Card>();
            public Queue<Card> PendingSummons = new Queue<Card>();
            public string SummonPlacement = "any";
            public Dictionary<Guid, int> TargetGenerations;
        }
        public void AttachEffects(ICardEffectProvider provider, ClockKind hostClock, ClockKind guestClock, IEnumerable<int> offFieldZones = null)
        {
            if (effects != null || Revision != 0) throw new InvalidOperationException("Effects must be attached once before play.");
            if (offFieldZones != null)
            {
                sharedOffFieldZones = offFieldZones.Distinct().OrderBy(id => id).ToArray();
                if (sharedOffFieldZones.Length == 0 || sharedOffFieldZones.Any(id => id < 0)) throw new ArgumentException("Shared off-field zones are required.");
            }
            effects = provider ?? throw new ArgumentNullException(nameof(provider));
            clocks[0].SetKind(hostClock); clocks[1].SetKind(guestClock);
            foreach (var card in cards) card.IsToken = effects.Rules(card.DefinitionId).IsToken;
            RefreshStats();
        }
        private EffectContext Context(Card source, EffectEvent kind, string reason = null) => new EffectContext {
            DefinitionId = source.DefinitionId, Owner = source.Owner, ActivePlayer = ActivePlayer, Turn = Turn,
            InstanceId = source.Id.ToString("N"), Time = source.Time, Power = source.Power, Node = source.NodeId,
            Cost = clocks[source.Owner].RemainingTime, Damage = clocks[source.Owner].DamagePointer,
            EmptyBoardCount = EmptySummonNodes(source.Owner).Length,
            ContractZoneCount = cards.Count(c => c.Owner == source.Owner && c.IsContract && c.Zone == TestCardZone.Contract),
            OpponentClock = clocks[1 - source.Owner].Kind, OpponentCost = clocks[1 - source.Owner].RemainingTime, OpponentDamage = clocks[1 - source.Owner].DamagePointer,
            OpponentHandCount = cards.Count(c => c.Owner != source.Owner && c.Zone == TestCardZone.Hand),
            PublicCards = cards.Where(c => PublicZone(c.Zone) && !c.HiddenAttachment).Select(MemoryCard).ToArray(),
            Clock = clocks[source.Owner].Kind, HandCount = cards.Count(c => c.Owner == source.Owner && c.Zone == TestCardZone.Hand),
            Event = kind, SourceZone = source.Zone, Reason = reason, Variables = VariableContexts.FromSnapshots(VariableSnapshots(source.Owner, source), source.Owner, source.Id),
            ContractName = cards.Where(c => c.Owner == source.Owner && c.IsContract && (c.Zone == TestCardZone.Board || c.Zone == TestCardZone.Contract)).Select(c => effects.Rules(c.DefinitionId).Name).FirstOrDefault() ?? "",
            OwnFieldNameCounts = cards.Where(c => c.Owner == source.Owner && (c.Zone == TestCardZone.Board || c.Zone == TestCardZone.OffField)).GroupBy(c => effects.Rules(c.DefinitionId).Name).ToDictionary(g => g.Key, g => g.Count()) };
        private bool PreparePlay(Card card, out string error)
        {
            error = null;
            if (effects == null) return true; // Legacy rules-only fixtures.
            var rule = effects.Rules(card.DefinitionId);
            if (!rule.HasPlay) return Fail("这张决策卡尚未配置可执行的 Lua onPlay，不能消耗费用空放。", out error);
            if (LimitReached(card, EffectEvent.Played)) return Fail("本回合已使用该效果。", out error);
            if (rule.OwnTurnOnly && card.Owner != ActivePlayer) return Fail("这张决策卡只能在自己的回合使用。", out error);
            try
            {
                if (!effects.CanPlay(Context(card, EffectEvent.Played))) return Fail("不满足 Lua 发动条件。", out error);
                // Build before payment so invalid scripts fail without spending resources.
                preparedPlay = ValidatePlan(effects.Build(Context(card, EffectEvent.Played, "decision")));
                return true;
            }
            catch (Exception e) { return Fail("Lua 发动校验失败：" + e.Message, out error); }
        }
        private IReadOnlyList<EffectInstruction> ValidatePlan(IReadOnlyList<EffectInstruction> plan)
        {
            if (plan == null || plan.Count > 32) throw new FormatException("Effect plan must contain at most 32 instructions.");
            foreach (var instruction in plan) effectRegistry.Validate(instruction, effects);
            return plan;
        }
        private IReadOnlyList<EffectInstruction> preparedPlay;
        public bool CanActivateCard(Guid id)
        {
            var source = cards.FirstOrDefault(c => c.Id == id);
            if (effects == null || source == null || source.Covered || source.Owner != ActivePlayer || Phase != TestTurnPhase.Main ||
                responseAttack != null || effectChoice != null || occupationNode >= 0 || Winner != -1) return false;
            var rule = effects.Rules(source.DefinitionId);
            if (!EffectsActive(source) || !rule.HasActivate || source.Zone != rule.ActivateZone || LimitReached(source, EffectEvent.Activated) || !CanAffordActivation(source, rule)) return false;
            try { return effects.CanActivate(Context(source, EffectEvent.Activated)); } catch { return false; }
        }
        private bool Activate(int player, Guid id, out string error)
        {
            error = null;
            var source = cards.FirstOrDefault(c => c.Id == id && c.Owner == player);
            if (effects == null || source == null || source.Covered || player != ActivePlayer || Phase != TestTurnPhase.Main ||
                responseAttack != null || occupationNode >= 0) return Fail("当前不能发动启动效果。", out error);
            var rule = effects.Rules(source.DefinitionId);
            if (!EffectsActive(source) || !rule.HasActivate || source.Zone != rule.ActivateZone || LimitReached(source, EffectEvent.Activated))
                return Fail("没有可发动的效果，或本回合已使用。", out error);
            IReadOnlyList<EffectInstruction> plan;
            try
            {
                if (!effects.CanActivate(Context(source, EffectEvent.Activated))) return Fail("不满足启动条件。", out error);
                plan = ValidatePlan(effects.Build(Context(source, EffectEvent.Activated)));
            }
            catch (Exception e) { return Fail("Lua 启动校验失败：" + e.Message, out error); }
            if (rule.ActivationCosts.Length > 0)
            {
                try { return BeginActivationPayment(source, plan, out error); }
                catch (Exception e) { return Fail("费用配置无效：" + e.Message, out error); }
            }
            if (!PayTime(player, rule.ActivateCost, source)) return Fail("启动费用不足。", out error);
            RecordUse(source, EffectEvent.Activated);
            Present(source, CardPresentationKind.Effect);
            PublishCardAction(source, EffectEvent.Activated, "activate");
            executions.Enqueue(new EffectExecution { Source = source, Steps = plan });
            DrainEffects(); RefundActionTime(); Revision++;
            return true;
        }
        // A field re-entry is a fresh rules instance. Network identity stays stable for synchronization.
        private void ResetFieldInstance(Card source)
        {
            activationCounts.RemoveWhere(key => key.EndsWith(":instance:" + source.Id, StringComparison.Ordinal));
            source.FieldGeneration++;
            source.SuppressedUntilTurn = source.LastRebuiltTurn = source.ProtectedUntilTurn = source.ProtectedAgainstPlayer = -1;
            foreach (var target in cards) target.Modifiers.RemoveAll(m => m.SourceBound && m.Source == source.Id);
            source.Modifiers.Clear();
            source.Variables.ClearAll();
            source.Tapped = false; source.Covered = false; source.StackOrder = 0;
        }
        // Name limits belong to the controller for the entire turn, across field re-entries.
        private string LimitKey(Card source, EffectEvent kind, bool byName, string ability = null, int? generation = null) =>
            source.Owner + ":" + kind + (string.IsNullOrEmpty(ability) ? "" : ":" + ability) + ":" + (byName ? "name:" + effects.Rules(source.DefinitionId).Name : "generation:" + (generation ?? source.FieldGeneration) + ":instance:" + source.Id);
        private bool LimitReached(Card source, EffectEvent kind, string ability = null, int? generation = null, bool? once = null, bool? named = null)
        {
            if (NameEffectsBlocked(source.Id)) return true;
            var rule = effects.Rules(source.DefinitionId);
            return (once ?? rule.OncePerTurn) && activationCounts.Contains(LimitKey(source, kind, false, ability, generation)) ||
                (named ?? rule.OncePerNamePerTurn) && activationCounts.Contains(LimitKey(source, kind, true, ability));
        }
        private void RecordUse(Card source, EffectEvent kind, string ability = null, int? generation = null, bool? once = null, bool? named = null)
        {
            var rule = effects.Rules(source.DefinitionId);
            if (once ?? rule.OncePerTurn) activationCounts.Add(LimitKey(source, kind, false, ability, generation));
            if (named ?? rule.OncePerNamePerTurn) activationCounts.Add(LimitKey(source, kind, true, ability));
        }
        private void QueueEffect(Card source, EffectEvent kind, string reason)
        {
            if (effects == null) return;
            PublishCardAction(source, kind, reason);
            if (kind == EffectEvent.Summoned || kind == EffectEvent.Destroyed) { QueueAutomatic(source, kind, reason); return; }
            if (LimitReached(source, kind)) return;
            try
            {
                var plan = kind == EffectEvent.Played && preparedPlay != null ? preparedPlay : ValidatePlan(effects.Build(Context(source, kind, reason)));
                if (kind == EffectEvent.Played)
                { preparedPlay = null; Present(source, CardPresentationKind.Decision); }
                if (plan.Count > 0)
                {
                    RecordUse(source, kind);
                    if (kind != EffectEvent.Played) Present(source, CardPresentationKind.Effect);
                    executions.Enqueue(new EffectExecution { Source = source, Steps = plan });
                }
            }
            catch (Exception e) { StopForScriptError(e); }
        }
        private void StopForScriptError(Exception e)
        {
            // No silent half-resolution: halt the test match explicitly on an authored script fault.
            executions.Clear(); pendingAutomatic.Clear(); pendingEvents.Clear(); eventContinuations.Clear(); automaticBatches.Clear(); orderingBatch = null; effectChoice = null; responseAttack = null; foresightCombat = null;
            Winner = 2; ResultReason = "效果执行错误，对局停止：" + e.Message;
            lastAction = ResultReason;
            AddLog(ActivePlayer, "效果执行错误，对局停止。请查看主机错误信息。");
        }
        private void DrainEffects()
        {
            RefreshPlayerAttachments();
            if (drainingEffects || effectChoice != null) return;
            drainingEffects = true;
            try
            {
                ResolveEmptyDecks();
                SealAutomaticBatch();
                while (Winner == -1 && effectChoice == null)
                {
                    if (executions.Count == 0) PrepareAutomaticBatch();
                    if (effectChoice != null) break;
                    if (executions.Count == 0)
                    {
                        if (automaticBatches.Count > 0) continue;
                        if (eventContinuations.Count == 0)
                        {
                            if (drawRules != null) { RecycleEmptyDeck(0); RecycleEmptyDeck(1); }
                            SealAutomaticBatch();
                            if (automaticBatches.Count > 0) continue;
                            break;
                        }
                        eventContinuations.Dequeue()(); BeginDestructionWindow(); SealAutomaticBatch(); continue;
                    }
                    if (++effectSteps > 512) throw new InvalidOperationException("Effect chain exceeded 512 operations.");
                    var execution = executions.Peek();
                    if (!execution.Started)
                    {
                        execution.Started = true;
                        if (execution.AutomaticEvent.HasValue)
                        {
                            if (LimitReached(execution.Source, execution.AutomaticEvent.Value, execution.AutomaticKey, execution.SourceGeneration, execution.OncePerTurn, execution.OncePerNamePerTurn)) { executions.Dequeue(); continue; }
                            if (execution.Costs.Length > 0)
                            {
                                if (!BeginPayment(execution.Source, execution.Steps, execution.Costs, 0, EffectEvent.Triggered, execution, out _))
                                { AddLog(execution.Source.Owner, "无法支付诱发费用，跳过该效果。"); executions.Dequeue(); }
                                continue;
                            }
                            RecordUse(execution.Source, execution.AutomaticEvent.Value, execution.AutomaticKey, execution.SourceGeneration, execution.OncePerTurn, execution.OncePerNamePerTurn);
                            Present(execution.Source, CardPresentationKind.Effect);
                        }
                    }
                    if (execution.Index >= execution.Steps.Count) { PublishMarkResolved(execution); executions.Dequeue(); SealAutomaticBatch(); continue; }
                    var step = ResolveVariableParameters(execution, execution.Steps[execution.Index++]);
                    RefreshStats();
                    if (Winner != -1) break;
                    eventListenersBefore = CaptureListeners(); eventCause = execution.Source;
                    try { effectRegistry.Execute(new MatchEffectExecutionContext(this, execution, step), step, effects); }
                    finally { eventListenersBefore = null; eventCause = null; }
                    BeginDestructionWindow();
                    ResolveEmptyDecks();
                    RefreshStats();
                    SealAutomaticBatch();
                }
                if (executions.Count == 0 && effectChoice == null && automaticBatches.Count == 0) effectSteps = 0;
            }
            catch (Exception e) { StopForScriptError(e); }
            finally { drainingEffects = false; }
        }
        private void ResolveChoice(Guid selected)
        {
            var execution = executions.Peek();
            if (execution.Selection != null) { ResolveSelection(execution, selected); DrainEffects(); RefreshStats(); return; }
            var chosen = cards.FirstOrDefault(c => c.Id == selected);
            if (chosen == null) AddLog(execution.Source.Owner, "放弃可选目标（或选择超时）。");
            else AddLog(execution.Source.Owner, "选择目标：" + CardLabel(chosen), PublicZone(chosen.Zone) && !chosen.HiddenAttachment ? -1 : execution.Source.Owner);
            execution.Selected = selected;
            SaveSet(execution, execution.PendingChoiceStore, selected == Guid.Empty ? Array.Empty<Guid>() : new[] { selected });
            execution.PendingChoiceStore = null;
            effectChoice = null;
            DrainEffects(); RefreshStats();
        }
        private EffectChoice ChoiceFor(int player) => effectChoice == null ? null : new EffectChoice {
            Player = effectChoice.Player, Prompt = effectChoice.Player == player ? effectChoice.Prompt : "等待对方选择效果目标",
            Optional = effectChoice.Optional, Seconds = effectChoice.Seconds,
            IsDeckView = effectChoice.IsDeckView,
            IsForesightOffer = effectChoice.IsForesightOffer, PublicView = effectChoice.PublicView,
            IsBoardPlacement = effectChoice.IsBoardPlacement, IsNumber = effectChoice.IsNumber, IsPayment = effectChoice.IsPayment,
            NumberMinimum = effectChoice.NumberMinimum, NumberMaximum = effectChoice.NumberMaximum,
            TriggerOptions = effectChoice.Player == player ? effectChoice.TriggerOptions.Select(t => new TriggerOption { Id = t.Id, Owner = t.Owner, DefinitionId = t.DefinitionId, Label = t.Label }).ToArray() : Array.Empty<TriggerOption>(),
            NameOptions = effectChoice.Player == player ? (string[])effectChoice.NameOptions.Clone() : Array.Empty<string>(),
            DeckPositions = effectChoice.Player == player ? (int[])effectChoice.DeckPositions.Clone() : Array.Empty<int>(),
            ViewedCards = effectChoice.Player == player || effectChoice.PublicView ? effectChoice.ViewedCards.Select(c => VisibleCopy(c, player)).ToArray() : Array.Empty<Card>(),
            ZoneCandidates = effectChoice.Player == player ? (int[])effectChoice.ZoneCandidates.Clone() : Array.Empty<int>(),
            Candidates = effectChoice.Player == player ? (Guid[])effectChoice.Candidates.Clone() : Array.Empty<Guid>() };
    }
}

