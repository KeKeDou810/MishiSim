using System.Linq;
namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private bool EffectsActive(Card card) => !card.HiddenAttachment && card.SuppressedUntilTurn < Turn && !PlayerFaceDown(card);
        private bool CanChooseByEffect(Card source, Card target)
        {
            if (selectionWards.Any(w => w.Target == target && (w.Player < 0 || w.Player == source.Owner) && SelectionWardActive(w))) return false;
            if (target.ProtectedUntilTurn < Turn || target.ProtectedAgainstPlayer != source.Owner || effects == null) return true;
            var type = effects.Rules(source.DefinitionId).Type;
            return type != "通常时魔" && type != "契约时魔" && type != "衍生物";
        }
    }
}
