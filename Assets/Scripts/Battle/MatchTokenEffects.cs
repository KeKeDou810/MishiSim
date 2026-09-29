using System;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void SpawnBoard(EffectExecution execution, EffectInstruction step)
        {
            if (EmptySummonNodes(execution.Source.Owner).Length == 0) return;
            var rule = effects.Rules(step.DefinitionId);
            var token = new Card(Guid.NewGuid(), step.DefinitionId, execution.Source.Owner, -1, rule.Power, false, TestCardZone.Exile, false,
                step.Amount > 0 ? step.Amount : rule.Time) { IsToken = true };
            cards.Add(token); SaveSet(execution, step.StoreAs, new[] { token.Id });
            BeginEffectSummon(execution, new[] { token }, step.Placement);
        }
        private void RemoveToken(EffectExecution execution, EffectInstruction step)
        {
            foreach (var card in ResolveEffectTargets(execution, step).Where(c => c.IsToken).ToArray())
            {
                var from = card.Zone; int node = card.NodeId;
                var observers = CaptureListeners();
                ResetFieldInstance(card); card.Zone = TestCardZone.Removed; card.NodeId = -1;
                if (from == TestCardZone.Board)
                {
                    var remaining = cards.Where(c => c.Zone == from && c.NodeId == node).OrderByDescending(c => c.StackOrder).ToArray();
                    for (int i = 0; i < remaining.Length; i++) remaining[i].Covered = i > 0;
                }
                PublishMove(card, from, "removeToken", observers);
            }
        }
    }
}
