using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class ActivationPayment
        {
            public Card Source;
            public EffectEvent Kind;
            public EffectExecution Pending;
            public IReadOnlyList<EffectInstruction> Plan;
            public ActivationCost[] Costs;
            public int Index, Time;
            public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
            public readonly List<Card> Discards = new List<Card>(), Destroyed = new List<Card>();
            public readonly Dictionary<Guid, int> Generations = new Dictionary<Guid, int>();
            public readonly List<(Card Card, ActivationCost Cost)> Other = new List<(Card, ActivationCost)>();
            public readonly List<(Card Card, ActivationCost Cost)> Selections = new List<(Card, ActivationCost)>();
            public readonly Dictionary<int, int> Counts = new Dictionary<int, int>();
            public int SelectedCount, SelectedTime;
            public double Seconds;
            public bool MidExecution;
            public IReadOnlyList<EffectInstruction> After;
        }
        private ActivationPayment activationPayment;
        private Card[] CostCandidates(Card source, ActivationCost cost, EffectEvent kind) => cards.Where(c => c.Owner == source.Owner &&
            (cost.Subject != "self" || c == source) && (cost.Subject != "other" || c != source) &&
            (cost.Kind == ActivationCostKind.MoveSelf ? c == source : cost.Zones.Length > 0 ? cost.Zones.Contains(c.Zone) : c.Zone == (cost.Zone ?? (cost.Kind == ActivationCostKind.Discard ? TestCardZone.Hand : TestCardZone.Board))) &&
            !c.Covered && (kind != EffectEvent.Played || c != source) &&
            (cost.Kind != ActivationCostKind.Destroy || c.Zone == TestCardZone.Board && !IsDestructionPrevented(c, "effect")) &&
            (cost.Kind != ActivationCostKind.RemoveToken || c.IsToken) &&
            (cost.Kind != ActivationCostKind.TokenTime || c.IsToken && c.Time >= cost.Minimum) &&
            (cost.Kind != ActivationCostKind.ReturnToDeck || CanReturnToDeck(c)) &&
            (string.IsNullOrEmpty(cost.Type) || effects.Rules(c.DefinitionId).Type == cost.Type) &&
            (string.IsNullOrEmpty(cost.Race) || effects.Rules(c.DefinitionId).Race == cost.Race) &&
            (string.IsNullOrEmpty(cost.Name) || (cost.NameMatch == "fuzzy" ? (effects.Rules(c.DefinitionId).Name ?? "").IndexOf(cost.Name, StringComparison.OrdinalIgnoreCase) >= 0 : effects.Rules(c.DefinitionId).Name == cost.Name))).ToArray();
        private bool CanPayMinimum(Card source, ActivationCost[] costs, int time, EffectEvent kind) =>
            clocks[source.Owner].RemainingTime >= time + costs.Where(c => c.Kind == ActivationCostKind.Time).Sum(c => c.Minimum) &&
            costs.Where(c => c.Kind != ActivationCostKind.Time).All(c => CostCandidates(source, c, kind).Length >= (c.Kind == ActivationCostKind.TokenTime ? 1 : c.Minimum));
        private bool CanAffordActivation(Card source, CardEffectRules rule) => CanPayMinimum(source, rule.ActivationCosts, rule.ActivateCost, EffectEvent.Activated);
        private bool BeginActivationPayment(Card source, IReadOnlyList<EffectInstruction> plan, out string error)
        {
            var rule = effects.Rules(source.DefinitionId);
            return BeginPayment(source, plan, rule.ActivationCosts, rule.ActivateCost, EffectEvent.Activated, null, out error);
        }
        private bool BeginPayment(Card source, IReadOnlyList<EffectInstruction> plan, ActivationCost[] costs, int time, EffectEvent kind, EffectExecution pending, out string error, bool midExecution = false, IReadOnlyList<EffectInstruction> after = null)
        {
            foreach (var cost in costs) cost.Validate();
            if (!CanPayMinimum(source, costs, time, kind))
                return Fail("无法完整支付发动费用。", out error);
            activationPayment = new ActivationPayment { Source = source, Plan = plan, Costs = costs, Time = time, Seconds = ChoiceBudget, Kind = kind, Pending = pending, MidExecution = midExecution, After = after };
            RequestPaymentChoice(); Revision++; error = null; return true;
        }
        private void RequestPaymentChoice()
        {
            var payment = activationPayment;
            while (payment.Index < payment.Costs.Length)
            {
                if (payment.Time + payment.Costs.Skip(payment.Index).Where(c => c.Kind == ActivationCostKind.Time).Sum(c => c.Minimum) > clocks[payment.Source.Owner].RemainingTime)
                { CancelPayment(); return; }
                var cost = payment.Costs[payment.Index];
                if (cost.Kind == ActivationCostKind.Time)
                {
                    if (cost.Minimum != cost.Maximum)
                    {
                        int reserve = payment.Costs.Skip(payment.Index + 1).Where(c => c.Kind == ActivationCostKind.Time).Sum(c => c.Minimum);
                        effectChoice = new EffectChoice { Player = payment.Source.Owner, IsNumber = true, IsPayment = true, Optional = true,
                            NumberMinimum = cost.Minimum, NumberMaximum = Math.Min(cost.Maximum, clocks[payment.Source.Owner].RemainingTime - payment.Time - reserve), Prompt = "选择支付的时间（取消不会扣除费用）", Seconds = payment.Seconds };
                        return;
                    }
                    AddTimeCost(cost.Minimum); continue;
                }
                if (cost.Minimum != cost.Maximum && !payment.Counts.ContainsKey(payment.Index))
                {
                    var available = CostCandidates(payment.Source, cost, payment.Kind).Count(c => !payment.Generations.ContainsKey(c.Id));
                    if (available < cost.Minimum) { CancelPayment(); return; }
                    effectChoice = new EffectChoice { Player = payment.Source.Owner, IsNumber = true, IsPayment = true, Optional = true,
                        NumberMinimum = cost.Minimum, NumberMaximum = Math.Min(cost.Maximum, available), Prompt = "选择支付的卡片数量", Seconds = payment.Seconds };
                    return;
                }
                int count = cost.Kind == ActivationCostKind.TokenTime ? 1 : payment.Counts.TryGetValue(payment.Index, out var selectedCount) ? selectedCount : cost.Minimum;
                if (payment.SelectedCount == count) { if (!string.IsNullOrEmpty(cost.StoreAs)) payment.Values[cost.StoreAs] = cost.StoreTime ? payment.SelectedTime : payment.SelectedCount; payment.Index++; payment.SelectedCount = payment.SelectedTime = 0; continue; }
                var candidates = CostCandidates(payment.Source, cost, payment.Kind).Where(c => !payment.Generations.ContainsKey(c.Id)).Select(c => c.Id).ToArray();
                if (candidates.Length == 0) { CancelPayment(); return; }
                effectChoice = new EffectChoice { Player = payment.Source.Owner, IsPayment = true, Optional = true, Candidates = candidates,
                    Prompt = "选择支付费用的卡片：" + cost.Kind, ViewedCards = candidates.Select(id => cards.Single(c => c.Id == id).Copy()).ToArray(), Seconds = payment.Seconds };
                return;
            }
            effectChoice = new EffectChoice { Player = payment.Source.Owner, IsNumber = true, IsPayment = true, Optional = true,
                Prompt = $"确认支付：时间 {payment.Time}，舍弃 {payment.Discards.Count} 张，破坏 {payment.Destroyed.Count} 个圆阵", Seconds = payment.Seconds };
        }
        private void AddTimeCost(int amount)
        {
            var cost = activationPayment.Costs[activationPayment.Index++]; activationPayment.Time += amount;
            if (!string.IsNullOrEmpty(cost.StoreAs)) activationPayment.Values[cost.StoreAs] = amount;
        }
        private void CancelPayment()
        {
            if (activationPayment?.Pending != null && !activationPayment.MidExecution && executions.Count > 0 && executions.Peek() == activationPayment.Pending) executions.Dequeue();
            if (activationPayment != null) AddLog(activationPayment.Source.Owner, "取消发动，未支付费用。");
            activationPayment = null; effectChoice = null; lastAction = "取消发动，未支付费用。";
        }
        private void ResolvePaymentCard(Guid id)
        {
            activationPayment.Seconds = effectChoice.Seconds;
            if (id == Guid.Empty) { CancelPayment(); return; }
            var payment = activationPayment; var card = cards.Single(c => c.Id == id);
            var cost = payment.Costs[payment.Index];
            payment.Selections.Add((card, cost));
            if (cost.Kind == ActivationCostKind.Discard) payment.Discards.Add(card); else if (cost.Kind == ActivationCostKind.Destroy) payment.Destroyed.Add(card); else payment.Other.Add((card, cost));
            payment.SelectedTime += card.Time;
            if (cost.PaySelectedTime) payment.Time += card.Time;
            payment.Generations[card.Id] = card.FieldGeneration; payment.SelectedCount++; effectChoice = null; RequestPaymentChoice();
        }
        private void ResolvePaymentNumber(int number)
        {
            activationPayment.Seconds = effectChoice.Seconds;
            if (number < 0) { CancelPayment(); return; }
            if (activationPayment.Index < activationPayment.Costs.Length) { if (activationPayment.Costs[activationPayment.Index].Kind == ActivationCostKind.Time) AddTimeCost(number); else activationPayment.Counts[activationPayment.Index] = number; effectChoice = null; RequestPaymentChoice(); return; }
            CommitPayment();
        }
        private void CommitPayment()
        {
            var payment = activationPayment; var source = payment.Source;
            bool sourceValid = payment.Pending != null || (payment.Kind == EffectEvent.Played
                ? source.Zone == TestCardZone.Hand
                : EffectsActive(source) && source.Zone == effects.Rules(source.DefinitionId).ActivateZone);
            bool valid = sourceValid &&
                (payment.Pending != null || !LimitReached(source, payment.Kind)) && clocks[source.Owner].RemainingTime >= payment.Time &&
                payment.Other.All(p => CostCandidates(source, p.Cost, payment.Kind).Contains(p.Card) && p.Card.FieldGeneration == payment.Generations[p.Card.Id]) &&
                payment.Selections.All(p => CostCandidates(source, p.Cost, payment.Kind).Contains(p.Card) && p.Card.FieldGeneration == payment.Generations[p.Card.Id]) &&
                payment.Destroyed.All(c => c.Zone == TestCardZone.Board && !c.Covered && c.Owner == source.Owner && c.FieldGeneration == payment.Generations[c.Id]);
            try { if (payment.Pending == null) valid &= payment.Kind == EffectEvent.Played ? effects.CanPlay(Context(source, EffectEvent.Played)) : effects.CanActivate(Context(source, EffectEvent.Activated)); }
            catch { valid = false; }
            if (!valid) { CancelPayment(); return; }
            activationPayment = null; effectChoice = null;
            var execution = payment.Pending ?? new EffectExecution { Source = source, Steps = payment.Plan, SourceGeneration = source.FieldGeneration, LockSourceGeneration = true };
            var activated = EventCard(source, true); var activationZone = source.Zone;
            foreach (var pair in payment.Values) execution.Variables.Set(pair.Key, EffectValue.Integer(pair.Value), source.Owner, false);
            // Selection and validation are complete; publish the entire payment as one atomic event group.
            eventListenersBefore = CaptureListeners(); eventCause = source;
            try
            {
                PayTime(source.Owner, payment.Time, source);
                if (payment.MidExecution)
                    execution.Steps = execution.Steps.Take(execution.Index).Concat(payment.After).Concat(execution.Steps.Skip(execution.Index)).ToArray();
                else if (payment.Pending != null) RecordUse(source, execution.AutomaticEvent.Value, execution.AutomaticKey, execution.SourceGeneration, execution.OncePerTurn, execution.OncePerNamePerTurn);
                else RecordUse(source, payment.Kind);
                foreach (var card in payment.Discards) MoveEffectCard(card, TestCardZone.Discard, "payment");
                foreach (var card in payment.Destroyed) DestroyPile(card, "payment");
                foreach (var item in payment.Other)
                {
                    if (item.Cost.Kind == ActivationCostKind.TokenTime) item.Card.Modifiers.Add(new StatModifier { Source = source.Id, Stat = "time", Amount = -item.Cost.Minimum, ExpiresAfterTurn = int.MaxValue });
                    else if (item.Cost.Kind == ActivationCostKind.ReturnToDeck) PlaceInDeck(new[] { item.Card }, item.Cost.Position == "top");
                    else MoveEffectCard(item.Card, item.Cost.Kind == ActivationCostKind.Exile ? TestCardZone.Exile : item.Cost.Kind == ActivationCostKind.RemoveToken ? TestCardZone.Removed : item.Cost.Destination, "payment");
                }
                if (payment.Kind == EffectEvent.Played)
                {
                    source.Zone = TestCardZone.Discard;
                    if (source.Owner == ActivePlayer) decisionsUsed++;
                    if (responseAttack != null)
                    {
                        responseAttack.Seconds[source.Owner] = Math.Min(drawRules.ResponseTimeSeconds, responseAttack.Seconds[source.Owner] + ActionTimeRefundSeconds);
                        responseAttack.Passes = 0; responseAttack.PriorityPlayer = 1 - source.Owner;
                    }
                    preparedPlay = null; Present(source, CardPresentationKind.Decision); PublishCardAction(source, EffectEvent.Played, "decision");
                }
                else
                {
                    Present(source, CardPresentationKind.Effect);
                    if (payment.Pending == null) Publish(new Events.ActivatedEvent(activated, activationZone, activationZone, "activate", activated));
                }
                if (payment.Pending == null) executions.Enqueue(execution);
            }
            finally { eventListenersBefore = null; eventCause = null; }
            DrainEffects(); RefundActionTime(); RefreshStats();
        }
    }
}
