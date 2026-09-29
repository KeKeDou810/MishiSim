using System.Collections.Generic;
namespace Mishi.Battle.Events
{
    [BattleEventType("ForesightResolved", "特效标记处理完成")]
    public sealed class ForesightResolvedEvent : CardBattleEvent
    {
        public EventCard[] Targets { get; }
        internal IReadOnlyList<EffectInstruction> Plan { get; }
        internal IReadOnlyList<EffectInstruction> AppliedCardSteps { get; }
        public ForesightResolvedEvent(EventCard card, EventCard attacker, EventCard[] targets, IReadOnlyList<EffectInstruction> plan, IReadOnlyList<EffectInstruction> appliedCardSteps)
            : base("ForesightResolved", card, TestCardZone.Revealed, TestCardZone.Revealed, "foresight", attacker)
        { Targets = targets; Plan = plan; AppliedCardSteps = appliedCardSteps; }
    }
}
