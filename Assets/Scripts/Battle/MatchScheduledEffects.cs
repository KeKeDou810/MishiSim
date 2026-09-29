using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class ScheduledEffect
        {
            public int Owner, RegisteredTurn;
            public string Timing;
            public EffectExecution Execution;
        }
        private readonly List<ScheduledEffect> scheduledEffects = new List<ScheduledEffect>();
        private void ExpireInvalidBattleSchedules() => scheduledEffects.RemoveAll(s => (s.Timing == "nextBattle" || s.Timing == "nextAttack" || s.Timing == "nextDefend") &&
            !cards.Any(c => c.Zone == TestCardZone.Board && s.Execution.Sets["scheduled"].Contains(c.Id) && s.Execution.TargetGenerations[c.Id] == c.FieldGeneration));
        private void ScheduleEffect(EffectExecution original, EffectInstruction step, IReadOnlyList<Card> targets)
        {
            if (scheduledEffects.Count >= 128) throw new InvalidOperationException("Too many pending schedules.");
            var deferred = new EffectExecution { Source = original.Source, SourceGeneration = original.Source.FieldGeneration,
                Steps = step.After.Select(s => s.Copy()).ToArray(), Selected = original.Selected,
                ScryTargets = original.ScryTargets.ToArray(), TargetGenerations = cards.ToDictionary(c => c.Id, c => c.FieldGeneration) };
            foreach (var pair in original.Sets) deferred.Sets[pair.Key] = pair.Value.ToArray();
            deferred.Sets["scheduled"] = targets.Select(c => c.Id).ToArray();
            foreach (var pair in original.Variables.VisibleTo(original.Source.Owner)) deferred.Variables.Set(pair.Key, pair.Value.Value, pair.Value.Owner, pair.Value.Public);
            scheduledEffects.Add(new ScheduledEffect { Owner = original.Source.Owner, RegisteredTurn = Turn, Timing = step.Timing, Execution = deferred });
        }
        private void RunSchedules(string timing, int player, Card attacker = null, Card defender = null)
        {
            bool Matches(ScheduledEffect s, Card card) => card != null && s.Execution.Sets["scheduled"].Contains(card.Id) && s.Execution.TargetGenerations[card.Id] == card.FieldGeneration;
            var due = scheduledEffects.Where(s => timing == "nextBattle"
                ? s.Timing == "nextBattle" && (Matches(s, attacker) || Matches(s, defender)) || s.Timing == "nextAttack" && Matches(s, attacker) || s.Timing == "nextDefend" && Matches(s, defender)
                : timing == "nextOwnMain" ? Turn > s.RegisteredTurn && (s.Timing == "nextMain" || s.Timing == "nextOwnMain" && s.Owner == player || s.Timing == "nextOpponentMain" && s.Owner != player) : s.Timing == timing && s.RegisteredTurn <= Turn).ToArray();
            foreach (var schedule in due) { scheduledEffects.Remove(schedule); executions.Enqueue(schedule.Execution); }
        }
    }
}
