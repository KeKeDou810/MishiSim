namespace Mishi.Battle.Events
{
    [BattleEventType("DamageTaken", "受到伤害")]
    public sealed class DamageTakenEvent : PlayerBattleEvent
    {
        public DamageTakenEvent(int player, int amount, int before, int after, string reason = null, EventCard cause = null) : base("DamageTaken", player, amount, before, after, reason, cause) { }
    }
}
