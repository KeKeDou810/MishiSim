using System;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private bool openingDealt;
        private readonly bool[] openingHandDecided = new bool[2];
        public bool OpeningHandsDecided => openingHandDecided.All(value => value);
        public bool OpeningHandDecided(int player) => player >= 0 && player < 2 && openingHandDecided[player];
        public void SetFirstPlayer(int player)
        {
            if (!OpeningPending || openingDealt || player < 0 || player > 1) throw new InvalidOperationException("先后手只能在发初始手牌前决定。");
            ActivePlayer = player;
            AddLog(player, "选择先攻。");
        }
        public bool CanRedrawOpeningHand(int player)
        {
            if (!OpeningPending || !openingDealt || player < 0 || player > 1 || openingHandDecided[player]) return false;
            var hand = cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Hand).ToArray();
            return hand.Length > 0 && !hand.Any(c => !c.IsDecision && !c.IsContract && c.BaseTime <= 4);
        }
        public bool DecideOpeningHand(int player, bool redraw, out Card[] revealed)
        {
            revealed = Array.Empty<Card>();
            if (!OpeningPending || !openingDealt || player < 0 || player > 1 || openingHandDecided[player]) return false;
            if (redraw && !CanRedrawOpeningHand(player)) return false;
            openingHandDecided[player] = true;
            if (!redraw) { AddLog(player, "保留初始手牌。"); Revision++; return true; }
            var hand = cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Hand).ToArray();
            revealed = hand.Select(c => c.Copy()).ToArray();
            AddLog(player, "公开初始手牌，使用一次重抽机会。");
            foreach (var card in hand) { Present(card, CardPresentationKind.Reveal); card.Zone = TestCardZone.Deck; }
            ShuffleDeck(player);
            // Setup is not an in-game draw/discard: no damage, draw triggers or discard triggers.
            DealSetupCards(player, hand.Length);
            Revision++;
            return true;
        }
        private void DealSetupCards(int player, int count)
        {
            foreach (var card in cards.Where(c => c.Owner == player && c.Zone == TestCardZone.Deck).Take(count)) card.Zone = TestCardZone.Hand;
        }
        public void CompleteOpeningHands()
        {
            if (!OpeningPending || !openingDealt || !OpeningHandsDecided) return;
            OpeningPending = false; Revision++;
            Publish(new Events.TurnStartedEvent(ActivePlayer, Turn, Phase));
            Publish(new Events.PhaseStartedEvent(ActivePlayer, Turn, Phase)); DrainEffects();
            lastAction = "初始手牌确认完成，开始对局。";
        }
    }
}
