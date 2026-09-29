namespace Mishi.Battle.Effects
{
    public sealed class ModifyBattleDamageEffect : KernelEffect
    {
        public override string Id => "ModifyBattleDamage";
        public override string Description => "修正本次攻击对玩家的伤害";
        public override string LuaExample => "{ op = \"ModifyBattleDamage\", amount = 1 }";
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) { if (instruction.Target == "self") context.ModifyBattleDamage(instruction.Amount); else context.ModifyBattleDamage(instruction.Amount, context.Targets); }
    }
}
