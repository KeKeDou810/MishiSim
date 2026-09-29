using System;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private static EffectInstruction[] BindScript(System.Collections.Generic.IReadOnlyList<EffectInstruction> plan, string definition) => plan.Select(s => {
            var copy = s.Copy(); copy.ScriptDefinitionId = definition; copy.After = BindScript(copy.After, definition); return copy;
        }).ToArray();
        private void RollDice(EffectExecution execution, EffectInstruction step)
        {
            int result = random.Next(1, step.Amount + 1);
            execution.Variables.Set(step.StoreAs, EffectValue.Integer(result), execution.Source.Owner, step.Reveal);
            presentations.Enqueue(new CardPresentation { Sequence = ++presentationSequence, Owner = execution.Source.Owner,
                Kind = CardPresentationKind.Dice, DiceSides = step.Amount, DiceResult = result, Audience = step.Reveal ? -1 : execution.Source.Owner });
            AddLog(execution.Source.Owner, "掷骰结果：" + result, step.Reveal ? -1 : execution.Source.Owner);
        }
        private void Continue(EffectExecution execution, EffectInstruction step)
        {
            if (!(effects is IScryEffectProvider provider)) throw new InvalidOperationException("Provider does not support continuations.");
            var context = Context(execution.Source, EffectEvent.Triggered, "continuation");
            if (!string.IsNullOrEmpty(step.ScriptDefinitionId)) context.DefinitionId = step.ScriptDefinitionId;
            PopulateEffectMemory(execution, context);
            context.ScryCards = execution.ScryTargets.Select(MemoryCard).ToArray();
            var plan = BindScript(ValidatePlan(provider.BuildScry(context, step.Callback)), context.DefinitionId);
            execution.Steps = execution.Steps.Take(execution.Index).Concat(plan).Concat(execution.Steps.Skip(execution.Index)).ToArray();
        }
        private void QueryCards(EffectExecution execution, EffectInstruction step)
        {
            var previous = step.Append && execution.Sets.TryGetValue(step.StoreAs, out var ids) ? ids : Array.Empty<Guid>();
            SaveSet(execution, step.StoreAs, previous.Concat(ExcludeTargets(execution, Candidates(execution.Source, step), step.Except).Select(c => c.Id)).ToArray());
        }
        private void InvokeDecision(EffectExecution execution, EffectInstruction step)
        {
            var plans = ResolveEffectTargets(execution, step).Where(c => c.IsDecision && (PublicZone(c.Zone) || c.Owner == execution.Source.Owner)).Select(c => {
                var context = Context(execution.Source, EffectEvent.Played, "invoke"); context.DefinitionId = c.DefinitionId;
                PopulateEffectMemory(execution, context);
                return BindScript(ValidatePlan(effects.Build(context)), c.DefinitionId);
            }).SelectMany(p => p).ToArray();
            // Keep the original controller/source: this is an effect copy, not a play or payment.
            execution.Steps = execution.Steps.Take(execution.Index).Concat(plans).Concat(execution.Steps.Skip(execution.Index)).ToArray();
        }
    }
}
