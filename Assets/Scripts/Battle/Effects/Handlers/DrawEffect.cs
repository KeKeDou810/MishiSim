namespace Mishi.Battle.Effects
{
    public sealed class DrawEffect : PlayerKernelEffect
    {
        public override string Id => "Draw";
        public override string Description => "指定玩家抽牌";
        public override string LuaExample => @"{ op = ""Draw"", amount = 1 }";
        public override void Validate(EffectInstruction instruction, ICardEffectProvider definitions)
        {
            base.Validate(instruction, definitions);
            EffectInstructionValidation.NonNegativeAmount(instruction);
        }
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, int player)
        { context.Draw(player, instruction.Amount); }
    }
}
