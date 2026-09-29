using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private Card redirectedTarget;
        private sealed class DestructionWard
        {
            public Card Target, Source;
            public int Generation, SourceGeneration, Until;
            public string Reason;
            public bool Bound, BattleBound;
        }
        private readonly List<DestructionWard> destructionWards = new List<DestructionWard>();
        private bool WardValid(DestructionWard ward) => ward.Until >= Turn && ward.Target.FieldGeneration == ward.Generation &&
            (!ward.Bound || ward.Source.FieldGeneration == ward.SourceGeneration && !ward.Source.Covered && EffectsActive(ward.Source) &&
                (ward.Source.Zone == TestCardZone.Board || ward.Source.Zone == TestCardZone.OffField));
        private bool IsDestructionPrevented(Card card, string reason) => destructionWards.Any(w => w.Target == card && WardValid(w) && (w.Reason == "any" || w.Reason == reason));
        private void PreventDestruction(EffectExecution execution, EffectInstruction step)
        {
            destructionWards.RemoveAll(w => !WardValid(w));
            if (destructionWards.Count >= 128) throw new InvalidOperationException("Too many destruction wards.");
            foreach (var target in ResolveEffectTargets(execution, step))
            {
                destructionWards.Add(new DestructionWard { Target = target, Generation = target.FieldGeneration, Source = execution.Source,
                    SourceGeneration = execution.Source.FieldGeneration, Reason = step.From, Bound = step.Duration == "source", BattleBound = step.Duration == "battle",
                    Until = step.Duration == "permanent" || step.Duration == "source" ? int.MaxValue : step.Duration == "nextTurn" ? Turn + 1 : Turn });
            }
        }
        private void RedirectAttack(EffectExecution execution, EffectInstruction step)
        {
            var target = ResolveEffectTargets(execution, step).FirstOrDefault(c => c.Zone == TestCardZone.Board && !c.Covered && c.Owner == execution.Source.Owner);
            if (target == null) return;
            if (execution.EventData is Events.AttackDeclaredEvent attack && attack.TargetPlayer == target.Owner) redirectedTarget = target;
            if (responseAttack != null && responseAttack.PriorityPlayer == target.Owner)
            {
                responseAttack.Target = target.Id; responseAttack.TargetNode = target.NodeId; responseAttack.PlayerTarget = false;
                if (declaredCombat != null) { declaredCombat.Target = target; declaredCombat.Node = target.NodeId; declaredCombat.TargetGeneration = target.FieldGeneration; }
            }
            if (foresightCombat != null && foresightCombat.Defender == target.Owner)
            { foresightCombat.Target = target; foresightCombat.Node = target.NodeId; foresightCombat.TargetGeneration = target.FieldGeneration; }
        }
    }
}
