using System;
using System.Collections.Generic;
using System.Linq;

namespace Mishi.Battle
{
    public enum CardPresentationKind { Decision, Effect, Reveal, Dice }
    public sealed class CardPresentation
    {
        public long Sequence;
        public string DefinitionId;
        public int Owner;
        public int DiceSides, DiceResult;
        public int Audience = -1;
        public CardPresentationKind Kind;
    }
    public sealed partial class NetworkTestMatch
    {
        private readonly List<(int Owner, int Turn, int Amount)> revealTimeAdjustments = new List<(int, int, int)>();
        private long presentationSequence;
        private readonly Queue<CardPresentation> presentations = new Queue<CardPresentation>();
        private void Present(Card card, CardPresentationKind kind)
        {
            if (kind == CardPresentationKind.Reveal && (card.Zone == TestCardZone.Deck || card.Zone == TestCardZone.Revealed))
            {
                revealTimeAdjustments.RemoveAll(m => m.Turn != Turn);
                int amount = revealTimeAdjustments.Where(m => m.Owner == card.Owner).Sum(m => m.Amount);
                if (amount != 0) { card.Modifiers.Add(new StatModifier { Source = card.Id, Stat = "time", Amount = amount, ExpiresAfterTurn = Turn }); RefreshStats(); }
            }
            AddLog(card.Owner, (kind == CardPresentationKind.Reveal ? "展示：" : kind == CardPresentationKind.Decision ? "使用决策卡：" : "发动效果：") + CardLabel(card));
            presentations.Enqueue(new CardPresentation { Sequence = ++presentationSequence, DefinitionId = card.DefinitionId, Owner = card.Owner, Kind = kind });
        }
        public CardPresentation[] DrainPresentations()
        {
            var result = presentations.ToArray(); presentations.Clear(); return result;
        }
        // Host-only integration seam for future foresight/reveal resolution; there is no client reveal command.
        public void RevealForPresentation(Guid cardId)
        {
            var card = cards.Single(c => c.Id == cardId);
            Present(card, CardPresentationKind.Reveal);
        }
    }
}
