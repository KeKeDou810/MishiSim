using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class DecisionCostModifier { public int Player, Amount, Until, Generation; public Card Source; public bool Bound, BattleBound; }
        private readonly List<DecisionCostModifier> decisionCostModifiers = new List<DecisionCostModifier>();
        public int EffectivePlayCost(System.Guid id)
        {
            var card = cards.FirstOrDefault(c => c.Id == id);
            return card == null ? 0 : card.IsDecision ? DecisionCost(card) : card.Time;
        }
        private void ModifyDecisionCost(EffectExecution execution, EffectInstruction step)
        {
            decisionCostModifiers.RemoveAll(m => m.Until < Turn || m.Bound && m.Source.FieldGeneration != m.Generation);
            if (decisionCostModifiers.Count >= 128) throw new System.InvalidOperationException("Too many decision cost modifiers.");
            decisionCostModifiers.Add(new DecisionCostModifier { Source = execution.Source, Generation = execution.Source.FieldGeneration,
                Bound = step.Duration == "source", BattleBound = step.Duration == "battle", Player = step.Side == "opponent" ? 1 - execution.Source.Owner : execution.Source.Owner, Amount = step.Amount,
                Until = step.Duration == "source" || step.Duration == "permanent" ? int.MaxValue : step.Duration == "nextTurn" ? Turn + 1 : Turn });
        }
        private int DecisionCost(Card card) => ClampStat((long)card.Time + decisionCostModifiers.Where(m => m.Player == card.Owner && m.Until >= Turn &&
            (!m.Bound || m.Generation == m.Source.FieldGeneration && !m.Source.Covered && EffectsActive(m.Source) && (m.Source.Zone == TestCardZone.Board || m.Source.Zone == TestCardZone.OffField))).Sum(m => (long)m.Amount));
    }
}
