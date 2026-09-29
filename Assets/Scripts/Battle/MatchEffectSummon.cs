using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private bool CanEffectSummon(Card card, int owner) => card.Owner == owner && !card.Covered &&
            (card.Zone == TestCardZone.Hand || card.Zone == TestCardZone.Contract || card.Zone == TestCardZone.Discard || card.Zone == TestCardZone.Deck || card.Zone == TestCardZone.Revealed || card.Zone == TestCardZone.Exile) &&
            (effects.Rules(card.DefinitionId).Type == "通常时魔" || card.IsContract || card.IsToken);
        private void BeginEffectSummon(EffectExecution execution, IReadOnlyList<Card> targets, string placement = "any")
        {
            execution.SummonPlacement = placement;
            execution.PendingSummons = new Queue<Card>(targets.Where(c => CanEffectSummon(c, execution.Source.Owner)));
            RequestSummon(execution);
        }
        private void RequestSummon(EffectExecution execution)
        {
            while (execution.PendingSummons.Count > 0)
            {
                var card = execution.PendingSummons.Peek(); var available = EmptySummonNodes(card.Owner).Where(n => execution.SummonPlacement == "any" ||
                    (execution.SummonPlacement.StartsWith("node:") ? n.ToString() == execution.SummonPlacement.Substring(5) :
                    (execution.SummonPlacement == "player" ? board.PlayerAt(n) == card.Owner : board.IsProtectedBy(n, card.Owner) && board.PlayerAt(n) != card.Owner))).ToArray();
                if (!CanEffectSummon(card, execution.Source.Owner) || available.Length == 0) { execution.PendingSummons.Dequeue(); continue; }
                effectChoice = new EffectChoice { Player = execution.Source.Owner, IsBoardPlacement = true,
                    Prompt = "选择登场的空圆阵", ZoneCandidates = available, Seconds = ChoiceBudget };
                return;
            }
        }
        private double ChoiceBudget => drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20;
        private void ResolveSummonPlacement(int node)
        {
            var execution = executions.Peek(); var card = execution.PendingSummons.Dequeue();
            var previous = card.Zone;
            ResetFieldInstance(card); card.Zone = TestCardZone.Board; card.NodeId = node; card.Tapped = false;
            if (card.IsContract) playerFlipped[card.Owner] = !playerFlipped[card.Owner];
            PublishMove(card, previous, "effectSummon");
            Publish(new Events.SummonedEvent(EventCard(card, true), previous, card.Zone, "effectSummon", EventCard(execution.Source)));
            QueueAutomatic(card, EffectEvent.Summoned, "effectSummon");
            effectChoice = null; RequestSummon(execution); DrainEffects(); RefreshStats();
        }
    }
}
