using System;
namespace Mishi.Battle.Effects
{
    public sealed class PayEffect : KernelEffect
    {
        public override string Id => "Pay";
        public override string Description => "效果处理中确认并支付费用，成功才执行 after";
        public override string LuaExample => @"{ op = ""Pay"", amount = 2, costs = {{ kind = ""Discard"", amount = 1 }}, after = {{ op = ""InvokeDecision"", target = ""selected"" }} }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        {
            if (step.Amount < 0 || step.Amount > 100 || step.Costs == null || step.Costs.Length > 8 || step.After.Count > 32 || !string.IsNullOrEmpty(step.AfterCallback))
                throw new FormatException("Pay requires bounded costs and an instruction array; use Continue inside after for Lua.");
            foreach (var cost in step.Costs) cost.Validate();
            foreach (var action in step.After) KernelEffectRegistry.CreateDefault().Validate(action, definitions);
        }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.Pay(step);
    }
}
