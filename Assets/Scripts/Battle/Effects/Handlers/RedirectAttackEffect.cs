using System;
namespace Mishi.Battle.Effects
{
    public sealed class RedirectAttackEffect : KernelEffect
    {
        public override string Id => "RedirectAttack";
        public override string Description => "将当前攻击转移到所选己方时魔";
        public override string LuaExample => "{ op = \"RedirectAttack\" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) {  }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.RedirectAttack(step);
    }
}
