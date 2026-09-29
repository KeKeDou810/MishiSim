namespace Mishi.Battle.Events
{
    [BattleEventType("Played", "使用决策卡")]
    public sealed class PlayedEvent : CardBattleEvent
    {
        public PlayedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Played", card, from, to, reason, cause) { }
    }
}
