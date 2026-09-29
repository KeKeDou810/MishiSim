namespace Mishi.Battle.Events
{
    [BattleEventType("DeckPositioned", "放置到卡顶／卡底")]
    public sealed class DeckPositionedEvent : CardBattleEvent
    {
        public string Position { get; }
        public DeckPositionedEvent(EventCard card, TestCardZone from, string position, EventCard cause)
            : base("DeckPositioned", card, from, TestCardZone.Deck, "returnToDeck", cause) { Position = position; }
        public override bool VisibleTo(int player) => Card.VisibleTo(player);
    }
}
