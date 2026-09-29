using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private readonly Dictionary<(int Player, string Name), int> blockedNames = new Dictionary<(int, string), int>();
        private sealed class SelectionWard
        { public Card Target, Source; public int Generation, SourceGeneration, Player, Until; public bool Bound, BattleBound; }
        private readonly List<SelectionWard> selectionWards = new List<SelectionWard>();
        public bool NameEffectsBlocked(Guid id)
        {
            var card = cards.FirstOrDefault(c => c.Id == id);
            return effects != null && card != null && blockedNames.TryGetValue((card.Owner, effects.Rules(card.DefinitionId).Name), out int until) && until >= Turn;
        }
        private void BlockName(EffectExecution execution, EffectInstruction step)
        {
            foreach (var key in blockedNames.Where(p => p.Value < Turn).Select(p => p.Key).ToArray()) blockedNames.Remove(key);
            if (blockedNames.Count >= 128) throw new InvalidOperationException("Too many name restrictions.");
            var keyName = (step.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner, string.IsNullOrEmpty(step.Name) ? effects.Rules(execution.Source.DefinitionId).Name : step.Name);
            int until = step.Duration == "nextTurn" ? Turn + 1 : Turn;
            blockedNames[keyName] = blockedNames.TryGetValue(keyName, out int existing) ? Math.Max(existing, until) : until;
        }
        private bool SelectionWardActive(SelectionWard ward) => ward.Generation == ward.Target.FieldGeneration && ward.Until >= Turn &&
            (!ward.Bound || ward.SourceGeneration == ward.Source.FieldGeneration && !ward.Source.Covered && EffectsActive(ward.Source) && (ward.Source.Zone == TestCardZone.Board || ward.Source.Zone == TestCardZone.OffField));
        private void ProtectSelection(EffectExecution execution, EffectInstruction step)
        {
            selectionWards.RemoveAll(w => !SelectionWardActive(w));
            if (selectionWards.Count >= 128) throw new InvalidOperationException("Too many selection wards.");
            foreach (var target in ResolveEffectTargets(execution, step)) selectionWards.Add(new SelectionWard { Target = target, Source = execution.Source,
                Generation = target.FieldGeneration, SourceGeneration = execution.Source.FieldGeneration, Player = step.Side == "any" ? -1 : step.Side == "own" ? execution.Source.Owner : 1 - execution.Source.Owner,
                Bound = step.Duration == "source", BattleBound = step.Duration == "battle", Until = step.Duration == "permanent" || step.Duration == "source" ? int.MaxValue : step.Duration == "nextTurn" ? Turn + 1 : Turn });
        }
    }
}
