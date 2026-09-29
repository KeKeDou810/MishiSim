namespace Mishi.Battle.Events
{
    [BattleEventType("AttackDeclared", "攻击宣言")]
    public sealed class AttackDeclaredEvent : CardBattleEvent
    {
        public EventCard Target { get; }
        public int TargetPlayer { get; }
        public int TargetNode { get; }
        public AttackDeclaredEvent(EventCard attacker, EventCard target, int targetPlayer, int targetNode)
            : base("AttackDeclared", attacker, TestCardZone.Board, TestCardZone.Board, target == null ? "player" : "unit", attacker)
        { Target = target; TargetPlayer = targetPlayer; TargetNode = targetNode; }
    }
}
