namespace Mishi.Battle.Effects
{
    public sealed class BlockNameEffect : KernelEffect
    {
        public override string Id => "BlockName";
        public override string Description => "禁止指定玩家发动同名卡的效果";
        public override string LuaExample => @"{ op = ""BlockName"", duration = ""turn"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions)
        { if (step.Side == "any" || step.Duration != "turn" && step.Duration != "nextTurn") throw new System.FormatException("BlockName needs one player and turn/nextTurn duration."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.BlockName(step);
    }
}
