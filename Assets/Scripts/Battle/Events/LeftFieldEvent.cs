namespace Mishi.Battle.Events
{
    [BattleEventType("LeftField", "离场")]
    public sealed class LeftFieldEvent : CardBattleEvent
    {
        public LeftFieldEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("LeftField", card, from, to, reason, cause) { }
    }
}
