namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void Pay(EffectExecution execution, EffectInstruction step)
        {
            if (!BeginPayment(execution.Source, execution.Steps, step.Costs, step.Amount, EffectEvent.Triggered, execution, out _, true, step.After))
                AddLog(execution.Source.Owner, "无法支付后续费用，跳过对应后续效果。");
        }
    }
}
