namespace Mishi.Battle.Events
{
    [BattleEventType("VariableChanged", "变量变化")]
    public sealed class VariableChangedEvent : BattleEvent
    {
        public VariableScope Scope { get; }
        public string Key { get; }
        public EffectValue Before { get; }
        public EffectValue After { get; }
        public EventCard Card { get; }
        public bool Public { get; }
        public VariableChangedEvent(int player, VariableScope scope, string key, EffectValue before, EffectValue after, bool isPublic, EventCard card, EventCard cause)
            : base("VariableChanged", player, "effect", cause)
        { Scope = scope; Key = key; Before = before; After = after; Public = isPublic; Card = card; }
        public override bool VisibleTo(int player) => (Public || Player == player) && (Card == null || Card.VisibleTo(player));
    }
}
