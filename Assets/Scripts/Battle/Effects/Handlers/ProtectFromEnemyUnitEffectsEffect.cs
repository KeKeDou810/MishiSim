namespace Mishi.Battle.Effects
{
    public sealed class ProtectFromEnemyUnitEffectsEffect : CardKernelEffect
    {
        public override string Id => "ProtectFromEnemyUnitEffects";
        public override string Description => "直到下个对方回合结束，目标不能被对方时魔效果选择";
        public override string LuaExample => "{ op = \"ProtectFromEnemyUnitEffects\", target = \"selected\" }";
        protected override void Apply(EffectExecutionContext context, EffectInstruction instruction, NetworkTestMatch.Card card)
        { if (card.Zone == TestCardZone.Board) context.ProtectFromEnemyUnitEffects(card); }
    }
}
