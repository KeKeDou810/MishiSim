using System;
namespace Mishi.Battle.Effects
{
    public sealed class RemoveTokenEffect : KernelEffect
    {
        public override string Id => "RemoveToken";
        public override string Description => "消灭衍生物，不触发破坏";
        public override string LuaExample => "{ op = \"RemoveToken\" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) {  }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.RemoveToken(step);
    }
}
