using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle.Events
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BattleEventTypeAttribute : Attribute
    {
        public string Id { get; }
        public string Label { get; }
        public BattleEventTypeAttribute(string id, string label) { Id = id; Label = label; }
    }
    public static class BattleEventRegistry
    {
        public static IReadOnlyDictionary<string, string> Definitions { get; } = typeof(BattleEvent).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(BattleEvent).IsAssignableFrom(t))
            .Select(t => (BattleEventTypeAttribute)Attribute.GetCustomAttribute(t, typeof(BattleEventTypeAttribute)))
            .Where(a => a != null).ToDictionary(a => a.Id, a => a.Label, StringComparer.Ordinal);
        public static void Validate(string id)
        { if (!Definitions.ContainsKey(id ?? "")) throw new FormatException("Unknown battle event: " + id); }
    }
    // Immutable event-time identity and public metadata, never a live Card reference.
    public sealed class EventCard
    {
        public Guid InstanceId { get; }
        public int Generation { get; }
        public string DefinitionId { get; }
        public string Name { get; }
        public string Type { get; }
        public string Race { get; }
        public string Faction { get; }
        public string Sign { get; }
        public TestCardZone Zone { get; }
        public int Owner { get; }
        public int Power { get; }
        public int Time { get; }
        public int Node { get; }
        public bool Public { get; }
        public EventCard(NetworkTestMatch.Card card, CardEffectRules rule, bool isPublic)
        { InstanceId = card.Id; Generation = card.FieldGeneration; DefinitionId = card.DefinitionId; Name = rule.Name;
          Type = rule.Type; Race = rule.Race; Faction = rule.Faction; Sign = rule.Sign; Zone = card.Zone; Owner = card.HiddenAttachment ? card.OriginalOwner : card.Owner; Power = card.Power; Time = card.Time; Node = card.NodeId; Public = isPublic; }
        public bool VisibleTo(int player) => Public || Owner == player;
    }
    public abstract class BattleEvent
    {
        public string Id { get; }
        public int Player { get; }
        public string Reason { get; }
        public EventCard Cause { get; }
        protected BattleEvent(string id, int player, string reason = null, EventCard cause = null)
        { Id = id; Player = player; Reason = reason ?? ""; Cause = cause; }
        public virtual bool VisibleTo(int player) => true;
    }
    public abstract class CardBattleEvent : BattleEvent
    {
        public EventCard Card { get; }
        public TestCardZone From { get; }
        public TestCardZone To { get; }
        protected CardBattleEvent(string id, EventCard card, TestCardZone from, TestCardZone to, string reason, EventCard cause)
            : base(id, card.Owner, reason, cause) { Card = card; From = from; To = to; }
    }
    public abstract class PlayerBattleEvent : BattleEvent
    {
        public int Amount { get; }
        public int Before { get; }
        public int After { get; }
        protected PlayerBattleEvent(string id, int player, int amount, int before, int after, string reason, EventCard cause)
            : base(id, player, reason, cause) { Amount = amount; Before = before; After = after; }
    }
    public abstract class PhaseBattleEvent : BattleEvent
    {
        public int Turn { get; }
        public TestTurnPhase Phase { get; }
        protected PhaseBattleEvent(string id, int player, int turn, TestTurnPhase phase) : base(id, player) { Turn = turn; Phase = phase; }
    }
}
