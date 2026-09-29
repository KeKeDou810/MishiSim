using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void ShuffleDeck(int player)
        {
            AddLog(player, "洗切牌库。");
            var deck = cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Deck).ToArray();
            for (int i = deck.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = deck[i]; deck[i] = deck[j]; deck[j] = swap;
            }
            int index = 0;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].Owner == player && cards[i].Zone == TestCardZone.Deck) cards[i] = deck[index++];
        }
        private void RecycleEmptyDeck(int player)
        {
            if (Winner >= 0) return;
            if (cards.Any(c => c.Owner == player && c.Zone == TestCardZone.Deck)) { emptyHandled[player] = false; return; }
            var discard = cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Discard).ToArray();
            bool alreadyDamaged = emptyHandled[player];
            if (alreadyDamaged && discard.Length == 0) return;
            AddLog(player, $"牌库耗尽：将弃牌区 {discard.Length} 张卡放回牌库{(alreadyDamaged ? "。" : "，受到 2 点伤害。")}");
            foreach (var card in discard) { card.Zone = TestCardZone.Deck; card.NodeId = -1; card.Tapped = false; card.Covered = false; card.StackOrder = 0; }
            ShuffleDeck(player);
            emptyHandled[player] = discard.Length == 0;
            // Rulebook Q&A: deck exhaustion advances both damage and cost pointers.
            if (!alreadyDamaged) ApplyDamage(player, 2, true, "deckEmpty");
        }
        private void ResolveEmptyDecks()
        {
            if (drawRules == null) return;
            RecycleEmptyDeck(0); RecycleEmptyDeck(1);
        }
    }
}
