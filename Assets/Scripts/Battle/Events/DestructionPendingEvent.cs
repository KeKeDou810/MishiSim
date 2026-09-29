using System;
namespace Mishi.Battle.Events
{
    [BattleEventType("DestructionPending", "即将被破坏")]
    public sealed class DestructionPendingEvent : CardBattleEvent
    {
        public Guid RequestId { get; }
        public DestructionPendingEvent(Guid requestId, EventCard card, string reason, EventCard cause)
            : base("DestructionPending", card, TestCardZone.Board, TestCardZone.Board, reason, cause) { RequestId = requestId; }
    }
}
