using System;
namespace Mishi.Battle.Effects
{
    public sealed class QueryCardsEffect : KernelEffect
    {
        public override string Id => "QueryCards";
        public override string Description => "将符合条件的可见卡保存到效果集合";
        public override string LuaExample => @"{ op = ""QueryCards"", zone = ""Discard"", type = ""决策卡"", storeAs = ""decisions"" }";
        public override void Validate(EffectInstruction step, ICardEffectProvider definitions) { EffectVariableStore.ValidateKey(step.StoreAs); if ((step.Zone == TestCardZone.Hand || step.Zone == TestCardZone.Deck) && step.Side != "own") throw new FormatException("Cannot query opponent hidden card identities."); }
        public override void Execute(EffectExecutionContext context, EffectInstruction step) => context.QueryCards(step);
    }
}
