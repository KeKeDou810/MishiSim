using System;
using System.Collections.Generic;

namespace Mishi.Battle.Events
{
    public sealed class EventSubscription
    {
        public string Id, Event, Label, Subject = "any", Side = "any", Key;
        public bool? OncePerTurn, OncePerNamePerTurn;
        public ActivationCost[] Costs = Array.Empty<ActivationCost>();
        public readonly EventCardFilter CardFilter = new EventCardFilter();
        public string Type { get => CardFilter.Type; set => CardFilter.Type = value; }
        public string Race { get => CardFilter.Race; set => CardFilter.Race = value; }
        public string Name { get => CardFilter.Name; set => CardFilter.Name = value; }
        public string NameMatch { get => CardFilter.NameMatch; set => CardFilter.NameMatch = value; }
        public string[] Types { get => CardFilter.Types; set => CardFilter.Types = value; }
        public TestCardZone ActiveZone = TestCardZone.Board;
        public TestCardZone? From, To;
        public TestTurnPhase? Phase;
        public VariableScope? Scope;
        public void Validate()
        {
            EffectVariableStore.ValidateKey(Id); BattleEventRegistry.Validate(Event);
            CardFilter.Validate();
            if (Subject != "self" && Subject != "other" && Subject != "any" || Side != "own" && Side != "opponent" && Side != "any")
                throw new FormatException("Invalid event subject/side selector.");
            if (ActiveZone != TestCardZone.Board && ActiveZone != TestCardZone.OffField && ActiveZone != TestCardZone.Contract && ActiveZone != TestCardZone.Discard && ActiveZone != TestCardZone.Exile && ActiveZone != TestCardZone.Player && ActiveZone != TestCardZone.Hand && ActiveZone != TestCardZone.Deck)
                throw new FormatException("Unsupported listener zone.");
            if ((ActiveZone == TestCardZone.Hand || ActiveZone == TestCardZone.Deck) && Subject != "self") throw new FormatException("Hidden-zone listeners must use subject=self.");
        }
        public bool Matches(BattleEvent data, Guid listener, int owner, TestCardZone zone, bool covered)
        {
            if (data.Id != Event || covered || !data.VisibleTo(owner) || zone != ActiveZone) return false;
            if (Side != "any" && data.Player != (Side == "own" ? owner : 1 - owner)) return false;
            var card = (data as CardBattleEvent)?.Card ?? (data as VariableChangedEvent)?.Card;
            if (Subject == "self" && card?.InstanceId != listener || Subject == "other" && (card == null || card.InstanceId == listener)) return false;
            if (!CardFilter.Matches(card, owner)) return false;
            if (From.HasValue && (data as CardBattleEvent)?.From != From || To.HasValue && (data as CardBattleEvent)?.To != To) return false;
            if (Phase.HasValue && (data as PhaseBattleEvent)?.Phase != Phase) return false;
            if (Scope.HasValue && (data as VariableChangedEvent)?.Scope != Scope) return false;
            if (!string.IsNullOrEmpty(Key) && (data as VariableChangedEvent)?.Key != Key) return false;
            return true;
        }
    }
    public interface IEventEffectProvider
    {
        IReadOnlyList<EventSubscription> Subscriptions(string definitionId, string eventId);
        IReadOnlyList<EffectInstruction> BuildTriggered(EffectContext context, string triggerId);
    }
}
