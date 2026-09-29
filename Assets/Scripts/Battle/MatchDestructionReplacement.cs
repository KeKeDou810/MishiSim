using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class DestructionReplacement
        { public Card Target, Substitute, Source; public int TargetGeneration, SubstituteGeneration, SourceGeneration, Until; public string Reason; public bool Bound, BattleBound; }
        private readonly List<DestructionReplacement> destructionReplacements = new List<DestructionReplacement>();
        private void ReplaceDestruction(EffectExecution execution, EffectInstruction step)
        {
            var substitute = cards.FirstOrDefault(c => c.Id == execution.Selected && c.Zone == TestCardZone.Board && !c.Covered);
            if (substitute == null) return;
            destructionReplacements.RemoveAll(r => r.Until < Turn || r.Target.FieldGeneration != r.TargetGeneration || r.Substitute.FieldGeneration != r.SubstituteGeneration);
            if (destructionReplacements.Count >= 128) throw new InvalidOperationException("Too many destruction replacements.");
            foreach (var target in ResolveEffectTargets(execution, step).Where(c => c != substitute && c.NodeId != substitute.NodeId))
                destructionReplacements.Add(new DestructionReplacement { Target = target, Substitute = substitute, TargetGeneration = target.FieldGeneration,
                    Source = execution.Source, SourceGeneration = execution.Source.FieldGeneration, Bound = step.Duration == "source", BattleBound = step.Duration == "battle",
                    SubstituteGeneration = substitute.FieldGeneration, Until = step.Duration == "nextTurn" ? Turn + 1 : step.Duration == "permanent" || step.Duration == "source" ? int.MaxValue : Turn, Reason = step.From });
        }
        private bool TryReplaceDestruction(Card target, string reason)
        {
            if (reason == "payment") return false;
            var replacement = destructionReplacements.FirstOrDefault(r => r.Target == target && r.Until >= Turn && r.TargetGeneration == target.FieldGeneration &&
                r.SubstituteGeneration == r.Substitute.FieldGeneration && r.Substitute.Zone == TestCardZone.Board && !r.Substitute.Covered &&
                !IsDestructionPrevented(r.Substitute, reason) && (r.Reason == "any" || r.Reason == reason) &&
                (!r.Bound || r.Source.FieldGeneration == r.SourceGeneration && !r.Source.Covered && EffectsActive(r.Source) && (r.Source.Zone == TestCardZone.Board || r.Source.Zone == TestCardZone.OffField)));
            if (replacement == null) return false;
            destructionReplacements.Remove(replacement);
            DestroyPile(replacement.Substitute, reason, false); return true;
        }
    }
}
