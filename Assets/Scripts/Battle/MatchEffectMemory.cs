using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        // Memory belongs to this execution, never to the card definition or the whole match.
        private static void SaveSet(EffectExecution execution, string key, Guid[] ids)
        {
            if (!string.IsNullOrEmpty(key)) execution.Sets[key] = ids.Distinct().ToArray();
        }
        private static Guid[] ReadSet(EffectExecution execution, string key)
        {
            if (key == "markTargets" && execution.EventData is Events.ForesightResolvedEvent mark) return mark.Targets.Select(c => c.InstanceId).ToArray();
            if (key == "selected") return execution.Selected == Guid.Empty ? Array.Empty<Guid>() : new[] { execution.Selected };
            if (key == "scry") return execution.ScryTargets.Select(c => c.Id).ToArray();
            if (execution.Sets.TryGetValue(key, out var ids)) return ids;
            throw new FormatException("Unknown effect set: " + key);
        }
        private IEnumerable<Card> ExcludeTargets(EffectExecution execution, IEnumerable<Card> targets, string except)
        {
            if (string.IsNullOrEmpty(except)) return targets;
            var excluded = new HashSet<Guid>(ReadSet(execution, except));
            return targets.Where(c => !excluded.Contains(c.Id));
        }
        private Card[] ResolveEffectTargets(EffectExecution execution, EffectInstruction step)
        {
            IEnumerable<Card> targets;
            if (step.Target == "cause" || step.Target == "attacker" || step.Target == "defender")
            {
                var subject = step.Target == "cause" ? execution.EventData?.Cause : step.Target == "attacker" ? (execution.EventData as Events.BattleEndedEvent)?.Attacker ?? (execution.EventData as Events.AttackDeclaredEvent)?.Card : (execution.EventData as Events.BattleEndedEvent)?.Target ?? (execution.EventData as Events.AttackDeclaredEvent)?.Target;
                targets = cards.Where(c => subject != null && subject.VisibleTo(execution.Source.Owner) && c.Id == subject.InstanceId && c.FieldGeneration == subject.Generation);
            }
            else if (step.Target == "event")
            {
                var subject = (execution.EventData as Events.CardBattleEvent)?.Card ?? (execution.EventData as Events.VariableChangedEvent)?.Card;
                targets = cards.Where(c => subject != null && subject.VisibleTo(execution.Source.Owner) && c.Id == subject.InstanceId && c.FieldGeneration == subject.Generation);
            }
            else if (step.Target == "set")
            {
                var ids = ReadSet(execution, step.FromSet);
                targets = ids.Select(id => cards.FirstOrDefault(c => c.Id == id)).Where(c => c != null);
            }
            else if (step.Target == "scry") targets = execution.ScryTargets.Where((c, i) => step.ScryIndex == 0 || step.ScryIndex == i + 1);
            else if (step.Target == "all") targets = Candidates(execution.Source, step);
            else if (step.Target == "selected") targets = cards.Where(c => c.Id == execution.Selected);
            else targets = (execution.AutomaticEvent.HasValue || execution.LockSourceGeneration) && execution.Source.FieldGeneration != execution.SourceGeneration
                ? Array.Empty<Card>() : new[] { execution.Source };
            return ExcludeTargets(execution, targets, step.Except).Where(c => c.Zone != TestCardZone.Removed &&
                (execution.TargetGenerations == null || execution.TargetGenerations.TryGetValue(c.Id, out var generation) && c.FieldGeneration == generation)).Distinct().ToArray();
        }
        private ScryCardInfo MemoryCard(Card card)
        {
            var rule = effects.Rules(card.DefinitionId);
            return new ScryCardInfo { InstanceId = card.Id.ToString("N"), Zone = card.Zone, Node = card.NodeId, ZoneEnteredTurn = card.ZoneEnteredTurn, LastRebuiltTurn = card.LastRebuiltTurn, DefinitionId = card.DefinitionId, Owner = card.Owner, Name = rule.Name,
                Type = rule.Type, Race = rule.Race, Sign = rule.Sign, Power = card.Power, Time = card.Time };
        }
        private void PopulateEffectMemory(EffectExecution execution, EffectContext context)
        {
            context.TriggerEvent = execution.EventData;
            context.Variables[VariableScope.Effect] = execution.Variables.VisibleTo(execution.Source.Owner).ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
            context.Results = context.Variables[VariableScope.Effect].Where(p => p.Value.Type == EffectValueType.String).ToDictionary(p => p.Key, p => p.Value.Text, StringComparer.Ordinal);
            context.Sets = execution.Sets.ToDictionary(pair => pair.Key,
                pair => pair.Value.Select(id => cards.FirstOrDefault(c => c.Id == id))
                    .Where(c => c != null && c.Zone != TestCardZone.Removed && (!c.HiddenAttachment && (c.Owner == execution.Source.Owner || PublicZone(c.Zone) || execution.ScryTargets.Contains(c)))).Select(MemoryCard).ToArray(), StringComparer.Ordinal);
        }
    }
}
