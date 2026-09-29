using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class PendingAutomatic
        {
            public readonly Guid Id = Guid.NewGuid();
            public Card Source;
            public int Generation;
            public bool? OncePerTurn, OncePerNamePerTurn;
            public EffectEvent Event;
            public Events.BattleEvent EventData;
            public string Key, Label;
            public IReadOnlyList<EffectInstruction> Steps;
            public ActivationCost[] Costs = Array.Empty<ActivationCost>();
        }
        private sealed class AutomaticBatch
        {
            public int FirstPlayer;
            public readonly List<PendingAutomatic> Remaining = new List<PendingAutomatic>();
            public readonly List<PendingAutomatic> Ordered = new List<PendingAutomatic>();
            public readonly double[] Seconds = { -1, -1 };
        }
        private sealed class AutomaticEventNotice
        {
            public Card Source;
            public EffectEvent Kind;
            public string Reason;
        }
        private readonly List<AutomaticEventNotice> pendingAutomatic = new List<AutomaticEventNotice>();
        private readonly Queue<AutomaticBatch> automaticBatches = new Queue<AutomaticBatch>();
        private AutomaticBatch orderingBatch;

        private void QueueAutomatic(Card source, EffectEvent kind, string reason)
        {
            if (!EffectsActive(source)) return;
            if (pendingAutomatic.Count >= 128) { StopForScriptError(new FormatException("Too many simultaneous trigger events.")); return; }
            pendingAutomatic.Add(new AutomaticEventNotice { Source = source, Kind = kind, Reason = reason });
        }
        private void CollectAutomatic(AutomaticBatch batch, AutomaticEventNotice notice)
        {
            var source = notice.Source; var kind = notice.Kind; var reason = notice.Reason;
            var context = Context(source, kind, reason);
            var plans = effects is IAutomaticEffectProvider provider ? provider.BuildAutomatic(context) :
                new[] { new AutomaticEffectPlan { Label = kind.ToString(), Steps = effects.Build(context) } };
            if (plans == null || plans.Count > 32) throw new FormatException("At most 32 automatic effects per event.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plan in plans)
            {
                if (!string.IsNullOrEmpty(plan.Id)) EffectVariableStore.ValidateKey(plan.Id);
                if (!ids.Add(plan.Id ?? "")) throw new FormatException("Duplicate automatic effect id.");
                var steps = ValidatePlan(plan.Steps);
                if (steps.Count == 0 || LimitReached(source, kind, plan.Id)) continue;
                if (batch.Remaining.Count >= 128) throw new FormatException("Too many simultaneous automatic effects.");
                batch.Remaining.Add(new PendingAutomatic { Source = source, Generation = source.FieldGeneration, Event = kind, Key = plan.Id,
                    Label = string.IsNullOrEmpty(plan.Label) ? kind.ToString() : plan.Label, Steps = steps, Costs = plan.Costs });
            }
        }
        // One instruction is one atomic event group. Triggered effects wait until the current execution completes.
        private void SealAutomaticBatch()
        {
            if (pendingAutomatic.Count == 0 && pendingEvents.Count == 0) return;
            var batch = new AutomaticBatch { FirstPlayer = ActivePlayer };
            foreach (var notice in pendingAutomatic) CollectAutomatic(batch, notice);
            CollectEventListeners(batch);
            pendingAutomatic.Clear();
            if (batch.Remaining.Count > 0) automaticBatches.Enqueue(batch);
        }
        private void PrepareAutomaticBatch()
        {
            if (orderingBatch == null)
            {
                if (automaticBatches.Count == 0) return;
                orderingBatch = automaticBatches.Dequeue();
            }
            while (orderingBatch.Remaining.Count > 0)
            {
                int player = orderingBatch.Remaining.Any(t => t.Source.Owner == orderingBatch.FirstPlayer)
                    ? orderingBatch.FirstPlayer : 1 - orderingBatch.FirstPlayer;
                var options = orderingBatch.Remaining.Where(t => t.Source.Owner == player).ToArray();
                if (orderingBatch.Seconds[player] == 0)
                { orderingBatch.Remaining.RemoveAll(t => t.Source.Owner == player); continue; }
                if (orderingBatch.Seconds[player] < 0)
                    orderingBatch.Seconds[player] = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20;
                effectChoice = new EffectChoice { Player = player, Seconds = orderingBatch.Seconds[player],
                    Prompt = "选择要发动的诱发效果（先选先处理），或放弃剩余效果",
                    TriggerOptions = options.Select(t => new TriggerOption { Id = t.Id, Owner = player,
                        DefinitionId = t.Source.DefinitionId, Label = t.Label }).ToArray() };
                return;
            }
            foreach (var trigger in orderingBatch.Ordered)
                executions.Enqueue(new EffectExecution { Source = trigger.Source, Steps = trigger.Steps,
                    AutomaticEvent = trigger.Event, AutomaticKey = trigger.Key, EventData = trigger.EventData, SourceGeneration = trigger.Generation,
                    OncePerTurn = trigger.OncePerTurn, OncePerNamePerTurn = trigger.OncePerNamePerTurn, Costs = trigger.Costs });
            orderingBatch = null;
        }
        private void SelectAutomatic(PendingAutomatic trigger)
        {
            AddLog(trigger.Source.Owner, $"选择诱发：{CardLabel(trigger.Source)} · {trigger.Label}");
            orderingBatch.Remaining.Remove(trigger); orderingBatch.Ordered.Add(trigger);
        }
        private void ResolveTriggerOrder(Guid id)
        {
            orderingBatch.Seconds[effectChoice.Player] = Math.Max(0, effectChoice.Seconds);
            if (id == Guid.Empty)
            {
                AddLog(effectChoice.Player, "放弃剩余诱发效果（或选择超时）。");
                orderingBatch.Remaining.RemoveAll(t => t.Source.Owner == effectChoice.Player);
            }
            else SelectAutomatic(orderingBatch.Remaining.Single(t => t.Id == id));
            effectChoice = null;
            PrepareAutomaticBatch();
            DrainEffects(); RefreshStats();
        }
    }
}
