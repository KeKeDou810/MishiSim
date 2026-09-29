namespace Mishi.Battle.Events
{
    [BattleEventType("Drawn", "抽卡")]
    public sealed class DrawnEvent : CardBattleEvent
    {
        public DrawnEvent(EventCard card, TestCardZone from, TestCardZone to, string reason = null, EventCard cause = null) : base("Drawn", card, from, to, reason, cause) { }
    }
}
