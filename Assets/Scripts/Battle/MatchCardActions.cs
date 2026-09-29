using System;
using System.Linq;
namespace Mishi.Battle
{
    [Flags]
    public enum CardActions { None = 0, Summon = 1, Overclock = 2, Move = 4, Attack = 8, Decision = 16, Activate = 32 }
    public sealed partial class NetworkTestMatch
    {
        public int DecisionsUsed => decisionsUsed;
        // Synchronize usage history, never menu permissions.
        public int EffectUseCount(Guid id, EffectEvent kind, bool byName)
        {
            var card = cards.FirstOrDefault(c => c.Id == id);
            return effects != null && card != null && activationCounts.Contains(LimitKey(card, kind, byName)) ? 1 : 0;
        }
    }
}
