using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private readonly EffectVariableStore[] playerVariables = { new EffectVariableStore(), new EffectVariableStore() };
        private readonly EffectVariableStore turnVariables = new EffectVariableStore(), matchVariables = new EffectVariableStore();

        private VariableSnapshot[] VariableSnapshots(int viewer, Card extraSource = null)
        {
            var result = new List<VariableSnapshot>();
            void Append(EffectVariableStore store, VariableScope scope, int owner, Guid cardId = default)
            {
                foreach (var pair in store.VisibleTo(viewer))
                    result.Add(new VariableSnapshot { Scope = scope, Owner = owner >= 0 ? owner : pair.Value.Owner,
                        CardId = cardId, Key = pair.Key, Value = pair.Value.Value });
            }
            for (int player = 0; player < 2; player++) Append(playerVariables[player], VariableScope.Player, player);
            Append(turnVariables, VariableScope.Turn, -1); Append(matchVariables, VariableScope.Match, -1);
            foreach (var card in cards.Where(c => !c.HiddenAttachment && (c == extraSource || c.Zone != TestCardZone.Deck && c.Zone != TestCardZone.Removed && (c.Zone != TestCardZone.Hand || c.Owner == viewer))))
                Append(card.Variables, VariableScope.Card, card.Owner, card.Id);
            return result.ToArray();
        }
        private EffectVariableStore Store(EffectExecution execution, VariableScope scope, int player, Card card)
        {
            switch (scope)
            {
                case VariableScope.Effect: return execution.Variables;
                case VariableScope.Card: return card?.Variables ?? throw new FormatException("Variable reference has no selected card.");
                case VariableScope.Player: return playerVariables[player];
                case VariableScope.Turn: return turnVariables;
                case VariableScope.Match: return matchVariables;
                default: throw new FormatException("Unknown variable scope.");
            }
        }
        private EffectValue ReadVariable(EffectExecution execution, VariableReference reference)
        {
            reference.Validate();
            int player = reference.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner;
            Card card = reference.Target == "selected" ? cards.FirstOrDefault(c => c.Id == execution.Selected) : execution.Source;
            return Store(execution, reference.Scope, player, card).Read(reference.Key);
        }
        private EffectInstruction ResolveVariableParameters(EffectExecution execution, EffectInstruction instruction)
        {
            if (instruction.AmountReference == null && instruction.ValueReference == null) return instruction;
            var resolved = instruction.Copy();
            if (resolved.AmountReference != null)
            { resolved.Amount = ReadVariable(execution, resolved.AmountReference).RequireInteger(); resolved.AmountReference = null; }
            if (resolved.ValueReference != null)
            { resolved.Value = ReadVariable(execution, resolved.ValueReference); resolved.ValueReference = null; }
            return resolved;
        }
        private IEnumerable<(EffectVariableStore Store, int Owner, Card Card)> VariableDestinations(EffectExecution execution, EffectInstruction step, IReadOnlyList<Card> targets)
        {
            if (step.Scope == VariableScope.Card)
            {
                foreach (var card in targets) yield return (card.Variables, card.Owner, card);
                yield break;
            }
            int owner = step.Scope == VariableScope.Player && step.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner;
            yield return (Store(execution, step.Scope, owner, execution.Source), owner, null);
        }
        private void WriteVariable(EffectExecution execution, EffectInstruction step, IReadOnlyList<Card> targets, bool add)
        {
            foreach (var destination in VariableDestinations(execution, step, targets))
            {
                var before = destination.Store.VisibleTo(destination.Owner).FirstOrDefault(p => p.Key == step.Key).Value;
                if (add) destination.Store.Add(step.Key, step.Value);
                else destination.Store.Set(step.Key, step.Value, destination.Owner, step.Visibility == "public");
                var after = destination.Store.VisibleTo(destination.Owner).First(p => p.Key == step.Key).Value;
                PublishVariable(step, destination.Owner, destination.Card, before, after, execution.Source);
            }
        }
        private void ClearVariable(EffectExecution execution, EffectInstruction step, IReadOnlyList<Card> targets)
        {
            foreach (var destination in VariableDestinations(execution, step, targets))
            {
                var before = destination.Store.VisibleTo(destination.Owner).FirstOrDefault(p => p.Key == step.Key).Value;
                destination.Store.Clear(step.Key);
                PublishVariable(step, destination.Owner, destination.Card, before, null, execution.Source);
            }
        }
        private void PublishVariable(EffectInstruction step, int owner, Card card, StoredEffectValue before, StoredEffectValue after, Card source)
        {
            if (step.Scope == VariableScope.Effect || before == null && after == null) return;
            if (before != null && after != null && before.Value.Type == after.Value.Type && before.Value.Text == after.Value.Text &&
                before.Value.Number == after.Value.Number && before.Value.Boolean == after.Value.Boolean) return;
            Publish(new Events.VariableChangedEvent(owner, step.Scope, step.Key, before?.Value, after?.Value,
                (before == null || before.Public) && (after == null || after.Public), EventCard(card), EventCard(source)));
        }
    }
}
