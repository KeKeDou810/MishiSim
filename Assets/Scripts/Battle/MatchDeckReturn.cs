using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class DeckReturnState
        {
            public readonly List<Card> Remaining = new List<Card>();
            public readonly List<Card> Top = new List<Card>(), Bottom = new List<Card>();
        }
        private bool CanReturnToDeck(Card card) => !card.IsToken &&
            (card.Zone == TestCardZone.Deck || card.Zone == TestCardZone.Hand ||
             card.Zone == TestCardZone.Discard || card.Zone == TestCardZone.Exile || card.Zone == TestCardZone.Revealed ||
             card.Zone == TestCardZone.Board && !card.Covered);

        private void BeginDeckReturn(EffectExecution execution, IReadOnlyList<Card> targets, string position)
        {
            var eligible = targets.Where(CanReturnToDeck).Distinct().ToArray();
            if (eligible.Length == 0) return;
            if (position != "choose")
            {
                // Input order is the resulting top-to-bottom order, including target=scry indices.
                PlaceInDeck(eligible, position == "top"); return;
            }
            execution.DeckReturn = new DeckReturnState();
            execution.DeckReturn.Remaining.AddRange(eligible);
            effectChoice = new EffectChoice {
                Player = execution.Source.Owner, IsDeckView = true,
                Prompt = "选择放回卡顶或卡底",
                Candidates = eligible.Select(c => c.Id).ToArray(), ViewedCards = eligible.Select(c => c.Copy()).ToArray(),
                DeckPositions = new[] { 1, 2 },
                Seconds = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20
            };
        }
        private void PlaceInDeck(IEnumerable<Card> targets, bool top)
        {
            var roots = targets.Where(CanReturnToDeck).Distinct().ToArray();
            var previousZones = roots.ToDictionary(c => c.Id, c => c.Zone);
            var observers = CaptureListeners();
            var members = roots.SelectMany(c => c.Zone == TestCardZone.Board ? cards.Where(m => m.Zone == TestCardZone.Board && m.NodeId == c.NodeId).OrderByDescending(m => m.StackOrder).ToArray() : new[] { c }).Distinct().ToArray();
            var contractOwners = members.Where(c => c.IsContract && c.Zone == TestCardZone.Board).Select(c => c.Owner).ToArray();
            foreach (var card in roots) if (card.Zone != TestCardZone.Deck) MoveEffectCard(card, TestCardZone.Deck, deferContractDraw: true);
            var returned = members.Where(c => c.Zone == TestCardZone.Deck).ToArray();
            foreach (var card in returned) cards.Remove(card);
            if (top) cards.InsertRange(0, returned); else cards.AddRange(returned);
            foreach (var card in returned) Publish(new Events.DeckPositionedEvent(EventCard(card, PublicZone(previousZones[card.Id])), previousZones[card.Id], top ? "top" : "bottom", EventCard(eventCause)), observers);
            if (drawRules != null) foreach (int owner in contractOwners) Draw(owner, 1);
        }
        private bool ValidDeckViewChoice(Guid id, int position) => effectChoice.DeckPositions.Contains(position) &&
            (executions.Peek().Scry != null ? id == Guid.Empty : effectChoice.Candidates.Contains(id));

        private void ResolveDeckView(Guid id, int position)
        {
            var execution = executions.Peek();
            if (execution.Scry != null) FinishScry(execution);
            else
            {
                var state = execution.DeckReturn;
                var card = state.Remaining.Single(c => c.Id == id);
                (position == 1 ? state.Top : state.Bottom).Add(card); state.Remaining.Remove(card);
                if (state.Remaining.Count > 0)
                {
                    effectChoice.Candidates = state.Remaining.Select(c => c.Id).ToArray();
                    effectChoice.ViewedCards = state.Remaining.Select(c => c.Copy()).ToArray();
                    return;
                }
                FinishDeckReturn(execution);
            }
            DrainEffects(); RefreshStats();
        }
        private void FinishDeckReturn(EffectExecution execution)
        {
            PlaceInDeck(execution.DeckReturn.Top, true);
            PlaceInDeck(execution.DeckReturn.Bottom, false);
            execution.DeckReturn = null; effectChoice = null;
        }
        private void ResolveDeckViewTimeout()
        {
            var execution = executions.Peek();
            if (execution.Scry != null) FinishScry(execution);
            else
            {
                // One timer covers all choices. Preserve committed groups and append the remaining targets on top.
                execution.DeckReturn.Top.AddRange(execution.DeckReturn.Remaining);
                FinishDeckReturn(execution);
            }
            DrainEffects(); RefreshStats();
        }
    }
}
