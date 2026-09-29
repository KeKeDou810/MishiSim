namespace Mishi.Battle.Events
{
    [BattleEventType("PhaseEnded", "阶段结束")]
    public sealed class PhaseEndedEvent : PhaseBattleEvent
    {
        public PhaseEndedEvent(int player, int turn, TestTurnPhase phase) : base("PhaseEnded", player, turn, phase) { }
    }
}
