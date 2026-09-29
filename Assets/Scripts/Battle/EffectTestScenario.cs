using System;

namespace Mishi.Battle
{
    [Serializable]
    public sealed class EffectTestScenario
    {
        public string Name = "单卡与组合测试", Instructions = "";
        public int Seed = 1, ActivePlayer, Turn = 2;
        public TestTurnPhase Phase = TestTurnPhase.Main;
        public int[] Cost = { 4, 4 }, Damage = { 4, 4 };
        public ClockKind[] Clocks = { ClockKind.White, ClockKind.White };
        public EffectTestCard[] Cards = Array.Empty<EffectTestCard>();
    }
    [Serializable]
    public sealed class EffectTestCard
    {
        public string Id;
        public int Owner, Node = -1, Count = 1, StackOrder;
        public TestCardZone Zone = TestCardZone.Hand;
        public bool Tapped;
    }
}
