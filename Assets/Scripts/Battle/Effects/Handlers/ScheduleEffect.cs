using System;
namespace Mishi.Battle.Effects
{
    public sealed class ScheduleEffect : KernelEffect
    {
        public override string Id => "Schedule";
        public override string Description => "登记回合结束、下个己方主要阶段或下次战斗的延迟处理";
        public override string LuaExample => "{ op = \"Schedule\", target = \"selected\", timing = \"turnEnd\", after = {{ op = \"ReturnToDeck\", target = \"set\", set = \"scheduled\", position = \"bottom\" }} }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.Timing != "turnEnd" && step.Timing != "nextOwnMain" && step.Timing != "nextMain" && step.Timing != "nextOpponentMain" && step.Timing != "nextBattle" && step.Timing != "nextAttack" && step.Timing != "nextDefend") throw new FormatException("Unknown schedule timing.");
            if (!string.IsNullOrEmpty(step.AfterCallback) || step.After.Count == 0 || step.After.Count > 32) throw new FormatException("Schedule needs a bounded instruction array.");
            foreach (var action in step.After)
            {
                if (action.OperationId == "Schedule") throw new FormatException("Recursive schedules are not supported.");
                KernelEffectRegistry.CreateDefault().Validate(action, definitions);
            }
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.Schedule(step);
    }
}
