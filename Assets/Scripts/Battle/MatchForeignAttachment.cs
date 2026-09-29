using System;
using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private static Card VisibleCopy(Card card, int viewer) => !card.HiddenAttachment || viewer == card.OriginalOwner ? card.Copy() :
            new Card(card.Id, "", card.Owner, card.NodeId, zone: card.Zone) { StackOrder = card.StackOrder, Covered = true, Tapped = true, HiddenAttachment = true };
        private void AttachUnder(EffectExecution execution, EffectInstruction step)
        {
            var host = ResolveEffectTargets(execution, step).FirstOrDefault(c => c.Zone == TestCardZone.Board && c.Owner == execution.Source.Owner && !c.Covered);
            if (host == null) return;
            var ids = ReadSet(execution, string.IsNullOrEmpty(step.FromSet) ? "selected" : step.FromSet);
            foreach (var card in cards.Where(c => ids.Contains(c.Id) && c.Zone == TestCardZone.Hand && !c.IsContract && effects.Rules(c.DefinitionId).Type != "玩家卡").ToArray())
            {
                ResetFieldInstance(card); card.HiddenAttachment = card.Owner != host.Owner; card.Owner = host.Owner;
                card.StackOrder = cards.Where(c => c.Zone == TestCardZone.Board && c.NodeId == host.NodeId).Min(c => c.StackOrder) - 1;
                card.Zone = TestCardZone.Board; card.NodeId = host.NodeId; card.Covered = true; card.Tapped = true;
                PublishMove(card, TestCardZone.Hand, "attachUnder");
            }
        }
    }
}
