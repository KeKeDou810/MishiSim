namespace Mishi.Battle.Events
{
    [BattleEventType("PhaseStarted", "阶段开始")]
    public sealed class PhaseStartedEvent : PhaseBattleEvent
    {
        public PhaseStartedEvent(int player, int turn, TestTurnPhase phase) : base("PhaseStarted", player, turn, phase) { }
    }
}
