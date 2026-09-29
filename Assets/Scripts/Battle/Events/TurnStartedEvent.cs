namespace Mishi.Battle.Events
{
    [BattleEventType("TurnStarted", "回合开始")]
    public sealed class TurnStartedEvent : PhaseBattleEvent
    {
        public TurnStartedEvent(int player, int turn, TestTurnPhase phase) : base("TurnStarted", player, turn, phase) { }
    }
}
