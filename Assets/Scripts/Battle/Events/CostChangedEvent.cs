namespace Mishi.Battle.Events
{
    [BattleEventType("CostChanged", "费用变化")]
    public sealed class CostChangedEvent : PlayerBattleEvent
    {
        public CostChangedEvent(int player, int amount, int before, int after, string reason = null, EventCard cause = null) : base("CostChanged", player, amount, before, after, reason, cause) { }
    }
}
