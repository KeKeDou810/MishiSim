using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle.Events;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class GrantedTrigger
        {
            public Card Target, Source;
            public int Generation, SourceGeneration, Until;
            public bool Bound, BattleBound;
            public bool Once, Named;
            public string Definition, Callback, Event;
            public ActivationCost[] Costs = Array.Empty<ActivationCost>();
            public Guid Id = Guid.NewGuid();
        }
        private readonly List<GrantedTrigger> grantedTriggers = new List<GrantedTrigger>();
        private void GrantTrigger(EffectExecution execution, EffectInstruction step)
        {
            grantedTriggers.RemoveAll(g => g.Until < Turn || g.Target.FieldGeneration != g.Generation);
            if (grantedTriggers.Count >= 128) throw new InvalidOperationException("Too many granted triggers.");
            foreach (var card in ResolveEffectTargets(execution, step).Where(c => c.Zone == TestCardZone.Board || c.Zone == TestCardZone.Player))
                grantedTriggers.Add(new GrantedTrigger { Target = card, Source = execution.Source, Generation = card.FieldGeneration,
                    SourceGeneration = execution.Source.FieldGeneration, Bound = step.Duration == "source", BattleBound = step.Duration == "battle", Definition = step.ScriptDefinitionId ?? execution.Source.DefinitionId,
                    Callback = step.Callback, Event = step.Key, Costs = step.Costs, Once = step.OncePerTurn ?? false, Named = step.OncePerNamePerTurn ?? false,
                    Until = step.Duration == "permanent" || step.Duration == "source" ? int.MaxValue : step.Duration == "nextTurn" ? Turn + 1 : Turn });
        }
        private void CollectGrantedTriggers(AutomaticBatch batch)
        {
            if (!(effects is IScryEffectProvider provider)) return;
            foreach (var notice in pendingEvents)
                foreach (var grant in grantedTriggers.Where(g => g.Event == notice.Data.Id && g.Until >= Turn && g.Target.FieldGeneration == g.Generation &&
                    (g.Target.Zone == TestCardZone.Board || g.Target.Zone == TestCardZone.Player) && !g.Target.Covered && EffectsActive(g.Target) &&
                    (!g.Bound || g.Source.FieldGeneration == g.SourceGeneration && !g.Source.Covered && EffectsActive(g.Source) && (g.Source.Zone == TestCardZone.Board || g.Source.Zone == TestCardZone.OffField))).ToArray())
                {
                    var context = Context(grant.Target, EffectEvent.Triggered, notice.Data.Reason); context.DefinitionId = grant.Definition; context.TriggerEvent = notice.Data;
                    var plan = BindScript(ValidatePlan(provider.BuildScry(context, grant.Callback)), grant.Definition);
                    string key = "grant_" + (grant.Named ? grant.Definition + ":" + grant.Callback : grant.Id.ToString("N"));
                    if (plan.Length == 0 || LimitReached(grant.Target, EffectEvent.Triggered, key, grant.Generation, grant.Once, grant.Named)) continue;
                    if (batch.Remaining.Count >= 128) throw new FormatException("Too many simultaneous triggers.");
                    batch.Remaining.Add(new PendingAutomatic { Source = grant.Target, Generation = grant.Generation, Event = EffectEvent.Triggered,
                        Key = key, Label = "授予效果", Steps = plan, Costs = grant.Costs, EventData = notice.Data, OncePerTurn = grant.Once, OncePerNamePerTurn = grant.Named });
                }
        }
    }
}
