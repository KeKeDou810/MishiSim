namespace Mishi.Battle.Events
{
    [BattleEventType("Activated", "发动效果")]
    public sealed class ActivatedEvent : CardBattleEvent
    {
        public ActivatedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Activated", card, from, to, reason, cause) { }
    }
}
