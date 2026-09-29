namespace Mishi.Battle.Events
{
    [BattleEventType("TurnEnded", "回合结束")]
    public sealed class TurnEndedEvent : PhaseBattleEvent
    {
        public TurnEndedEvent(int player, int turn, TestTurnPhase phase) : base("TurnEnded", player, turn, phase) { }
    }
}
