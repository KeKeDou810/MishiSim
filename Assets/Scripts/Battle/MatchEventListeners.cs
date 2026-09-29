using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle.Events;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private sealed class ListenerState
        { public Card Card; public TestCardZone Zone; public bool Covered; public int Generation; }
        private sealed class EventNotice
        { public BattleEvent Data; public ListenerState[] Listeners; }
        private readonly List<EventNotice> pendingEvents = new List<EventNotice>();
        private readonly Queue<Action> eventContinuations = new Queue<Action>();
        private ListenerState[] eventListenersBefore;
        private Card eventCause;
        private ListenerState[] CaptureListeners() => cards.Select(c => new ListenerState { Card = c, Zone = c.Zone, Covered = c.Covered || !EffectsActive(c), Generation = c.FieldGeneration }).ToArray();
        private static bool PublicZone(TestCardZone zone) => zone != TestCardZone.Hand && zone != TestCardZone.Deck && zone != TestCardZone.Removed;
        private EventCard EventCard(Card card, bool? isPublic = null) => card == null || effects == null ? null : new EventCard(card, effects.Rules(card.DefinitionId), !card.HiddenAttachment && (isPublic ?? PublicZone(card.Zone)));
        private void Publish(BattleEvent data, ListenerState[] before = null)
        {
            LogEvent(data);
            if (!(effects is IEventEffectProvider) || OpeningPending || Winner >= 0 || data == null) return;
            if (pendingEvents.Count >= 256) { StopForScriptError(new InvalidOperationException("Event batch exceeded 256 events.")); return; }
            // Pre-mutation observers preserve simultaneous leave-field triggers. New entrants are added afterward.
            var previous = before ?? eventListenersBefore ?? Array.Empty<ListenerState>();
            var listeners = previous.Concat(CaptureListeners()).GroupBy(c => (c.Card.Id, c.Zone)).Select(g => g.First()).ToArray();
            pendingEvents.Add(new EventNotice { Data = data, Listeners = listeners });
        }
        private void PublishMove(Card card, TestCardZone from, string reason, ListenerState[] before = null, string position = null)
        {
            if (from != card.Zone) card.ZoneEnteredTurn = Turn;
            if (effects == null || from == card.Zone) return;
            var subject = EventCard(card, PublicZone(from) || PublicZone(card.Zone));
            var cause = EventCard(eventCause);
            Publish(new CardMovedEvent(subject, from, card.Zone, reason, cause, position), before);
            if ((from == TestCardZone.Board || from == TestCardZone.OffField) && card.Zone != TestCardZone.Board && card.Zone != TestCardZone.OffField)
                Publish(new LeftFieldEvent(subject, from, card.Zone, reason, cause), before);
            if (from == TestCardZone.Hand && card.Zone == TestCardZone.Discard && (reason == "discard" || reason == "payment"))
                Publish(new DiscardedEvent(subject, from, card.Zone, reason, cause), before);
        }
        private void CollectEventListeners(AutomaticBatch batch)
        {
            if (!(effects is IEventEffectProvider provider)) { pendingEvents.Clear(); return; }
            foreach (var notice in pendingEvents)
                foreach (var listener in notice.Listeners)
                    foreach (var subscription in provider.Subscriptions(listener.Card.DefinitionId, notice.Data.Id))
                    {
                        subscription.Validate();
                        if (!subscription.Matches(notice.Data, listener.Card.Id, listener.Card.Owner, listener.Zone, listener.Covered)) continue;
                        var context = Context(listener.Card, EffectEvent.Triggered, notice.Data.Reason);
                        context.TriggerEvent = notice.Data;
                        var steps = ValidatePlan(provider.BuildTriggered(context, subscription.Id));
                        string key = notice.Data.Id + ":" + subscription.Id;
                        if (steps.Count == 0 || LimitReached(listener.Card, EffectEvent.Triggered, key, listener.Generation, subscription.OncePerTurn, subscription.OncePerNamePerTurn)) continue;
                        if (batch.Remaining.Count >= 128) throw new FormatException("Too many simultaneous automatic effects.");
                        batch.Remaining.Add(new PendingAutomatic { Source = listener.Card, Generation = listener.Generation, Event = EffectEvent.Triggered, Key = key,
                            OncePerTurn = subscription.OncePerTurn, OncePerNamePerTurn = subscription.OncePerNamePerTurn,
                            Label = subscription.Label ?? subscription.Id, Steps = steps, EventData = notice.Data, Costs = subscription.Costs });
                    }
            CollectGrantedTriggers(batch);
            pendingEvents.Clear();
        }
    }
}
