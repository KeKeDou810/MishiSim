using System;

namespace Mishi.Networking
{
    public static class RoomRuleLimits
    {
        public static void Validate(int openingHand, int drawPerTurn, int turnSeconds, int refundSeconds, int responseSeconds, int deckSize)
        {
            if (openingHand < 0 || openingHand >= deckSize || drawPerTurn < 0 || drawPerTurn > 20 ||
                turnSeconds < 1 || turnSeconds > 3600 || refundSeconds < 0 || refundSeconds > turnSeconds || responseSeconds < 1 || responseSeconds > 300)
                throw new ArgumentException($"初始手牌：0～{deckSize - 1}；每回合抽牌：0～20；回合时间：1～3600 秒；返还时间：0～回合时间；响应时间：1～300 秒。");
        }
    }
}
