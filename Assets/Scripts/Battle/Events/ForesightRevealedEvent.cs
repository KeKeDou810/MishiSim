namespace Mishi.Battle.Events
{
    [BattleEventType("ForesightRevealed", "未来视公开")]
    public sealed class ForesightRevealedEvent : CardBattleEvent
    {
        public EventCard Revealed { get; }
        public ForesightRevealedEvent(EventCard attacker, EventCard revealed)
            : base("ForesightRevealed", attacker, TestCardZone.Board, TestCardZone.Board, "foresight", attacker) { Revealed = revealed; }
    }
}
