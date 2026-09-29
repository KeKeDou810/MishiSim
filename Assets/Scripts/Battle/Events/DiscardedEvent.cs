namespace Mishi.Battle.Events
{
    [BattleEventType("Discarded", "弃牌")]
    public sealed class DiscardedEvent : CardBattleEvent
    {
        public DiscardedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Discarded", card, from, to, reason, cause) { }
    }
}
