using System;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private readonly int[] playerHomeNodes = { -1, -1 };
        private sealed class PlayerAttachment { public Card Source; public int Generation; }
        private readonly PlayerAttachment[] playerAttachments = new PlayerAttachment[2];
        public void AddPlayerCards(int player, int node, params Card[] pair)
        {
            if (Revision != 0 || effects == null || player < 0 || player > 1 || pair.Length != 2 ||
                cards.Any(c => c.Owner == player && c.Zone == TestCardZone.Player) ||
                pair.Any(c => effects.Rules(c.DefinitionId).Type != "玩家卡")) throw new ArgumentException("Invalid initial player card pair.");
            playerHomeNodes[player] = node;
            for (int i = 0; i < 2; i++) cards.Add(new Card(pair[i].Id == Guid.Empty ? Guid.NewGuid() : pair[i].Id,
                pair[i].DefinitionId, player, node, 0, false, TestCardZone.Player) { StackOrder = i });
        }
        private void AttachPlayerCards(Card source)
        {
            if (source.Zone != TestCardZone.Board || source.Covered || !source.IsContract || !EffectsActive(source) || playerHomeNodes[source.Owner] < 0) return;
            playerAttachments[source.Owner] = new PlayerAttachment { Source = source, Generation = source.FieldGeneration };
            RefreshPlayerAttachments();
        }
        private void RefreshPlayerAttachments()
        {
            for (int owner = 0; owner < 2; owner++)
            {
                var attachment = playerAttachments[owner];
                if (attachment != null && (attachment.Source.Zone != TestCardZone.Board || attachment.Source.Covered ||
                    attachment.Source.FieldGeneration != attachment.Generation || !EffectsActive(attachment.Source)))
                    playerAttachments[owner] = attachment = null;
                foreach (var player in cards.Where(c => c.Zone == TestCardZone.Player && c.Owner == owner))
                {
                    player.NodeId = attachment?.Source.NodeId ?? playerHomeNodes[owner];
                    player.AttachedTo = attachment?.Source.Id ?? Guid.Empty;
                }
            }
        }
        private bool PlayerIsExposed(int owner)
        {
            RefreshPlayerAttachments();
            return playerAttachments[owner] == null && !cards.Any(c => c.Owner == owner && c.Zone == TestCardZone.Board && board.IsProtectedBy(c.NodeId, owner));
        }
        private bool PlayerFaceDown(Card card) => card.Zone == TestCardZone.Player && ((card.StackOrder == 1) ^ playerFlipped[card.Owner]);
        public Card[] PlayerCards => cards.Where(c => c.Zone == TestCardZone.Player).Select(c => { var copy = c.Copy(); copy.Covered = PlayerFaceDown(c); return copy; }).ToArray();
        private int[] EmptySummonNodes(int owner) => nodes.Where(n => !board.IsOpposingProtectedNode(n, owner) && !cards.Any(c => c.Zone == TestCardZone.Board && c.NodeId == n)).OrderBy(n => n).ToArray();
    }
}
