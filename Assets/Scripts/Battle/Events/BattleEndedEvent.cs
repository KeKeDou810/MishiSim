namespace Mishi.Battle.Events
{
    [BattleEventType("BattleEnded", "战斗结束")]
    public sealed class BattleEndedEvent : CardBattleEvent
    {
        public EventCard Attacker => Card;
        public EventCard Target { get; }
        public int TargetPlayer { get; }
        public bool Cancelled { get; }
        public BattleEndedEvent(EventCard attacker, EventCard target, int targetPlayer, bool cancelled)
            : base("BattleEnded", attacker, TestCardZone.Board, TestCardZone.Board, cancelled ? "cancelled" : "battle", attacker)
        { Target = target; TargetPlayer = targetPlayer; Cancelled = cancelled; }
    }
}
