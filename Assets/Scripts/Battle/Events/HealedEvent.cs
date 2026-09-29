namespace Mishi.Battle.Events
{
    [BattleEventType("Healed", "回复")]
    public sealed class HealedEvent : PlayerBattleEvent
    {
        public HealedEvent(int player, int amount, int before, int after, string reason = null, EventCard cause = null) : base("Healed", player, amount, before, after, reason, cause) { }
    }
}
