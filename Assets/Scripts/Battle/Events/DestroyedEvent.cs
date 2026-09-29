namespace Mishi.Battle.Events
{
    [BattleEventType("Destroyed", "破坏")]
    public sealed class DestroyedEvent : CardBattleEvent
    {
        public DestroyedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Destroyed", card, from, to, reason, cause) { }
    }
}
