namespace Mishi.Battle.Events
{
    [BattleEventType("Summoned", "登场")]
    public sealed class SummonedEvent : CardBattleEvent
    {
        public SummonedEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Summoned", card, from, to, reason, cause) { }
    }
}
