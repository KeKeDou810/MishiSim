namespace Mishi.Battle.Events
{
    [BattleEventType("CardMoved", "区域变化")]
    public sealed class CardMovedEvent : CardBattleEvent
    {
        public string Position { get; }
        public CardMovedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null, string position = null) : base("CardMoved", card, from, to, reason, cause) { Position = position; }
    }
}
