using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void PublishMarkResolved(EffectExecution execution)
        {
            if (execution.ResolvedMark == null || execution.ResolvedMark.Count == 0) return;
            Publish(new Events.ForesightResolvedEvent(EventCard(execution.Source, true), EventCard(execution.Foresight?.Attacker, true),
                execution.Affected.Values.ToArray(), execution.ResolvedMark, execution.AppliedMarkSteps.ToArray()));
            execution.ResolvedMark = null;
        }
        private void CopyForesight(EffectExecution execution, EffectInstruction step)
        {
            if (!(execution.EventData is Events.ForesightResolvedEvent mark)) return;
            SaveSet(execution, "markTargets", mark.Targets.Select(t => t.InstanceId).ToArray());
            var plan = mark.Plan;
            if (step.Target != "self")
            {
                SaveSet(execution, "copiedMarkTargets", ResolveEffectTargets(execution, step).Where(c => c.Zone == TestCardZone.Board && !c.Covered).Select(c => c.Id).ToArray());
                plan = mark.AppliedCardSteps;
            }
            execution.Steps = execution.Steps.Take(execution.Index).Concat(plan.Select(s => s.Copy())).Concat(execution.Steps.Skip(execution.Index)).ToArray();
        }
    }
}
