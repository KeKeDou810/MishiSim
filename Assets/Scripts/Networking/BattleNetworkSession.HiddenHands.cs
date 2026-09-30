using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        // Anonymous presentation only: never bind definitions or host instance identities.
        private readonly List<BattleCard> opponentHandBacks = new List<BattleCard>();
        private readonly List<BattleCard> spectatorHandBacks = new List<BattleCard>();

        private void LayoutHiddenHands()
        {
            if (!snapshot.HasValue || Camera.main == null || cardPrefab == null) return;
            var state = snapshot.Value;
            LayoutHiddenHand(opponentHandBacks, HiddenHandCount(state, 1 - state.You), 1 - state.You, true);
            LayoutHiddenHand(spectatorHandBacks, state.Spectator ? HiddenHandCount(state, state.You) : 0, state.You, false);
            bool hovered = opponentHandBacks.Any(card => card != null && card.GetComponent<BattleCardPointer>().IsHovered);
            playerHud?.SetOpponentHandHovered(hovered);
        }

        public static int HiddenHandCount(NetworkSnapshot state, int player)
        {
            if (state.HandCounts != null && player >= 0 && player < state.HandCounts.Length)
                return Mathf.Max(0, state.HandCounts[player]);
            return Mathf.Max(0, player == state.You ? state.OwnHand?.Length ?? 0 : state.OpponentHandCount);
        }

        private void LayoutHiddenHand(List<BattleCard> backs, int count, int owner, bool upper)
        {
            while (backs.Count > count)
            {
                int index = backs.Count - 1;
                Destroy(backs[index].gameObject); backs.RemoveAt(index);
            }
            var camera = Camera.main;
            var viewport = camera.pixelRect;
            float height = Mathf.Min(110, viewport.height * .13f);
            if (upper) playerHud?.PositionOpponentHandCount(camera, viewport.yMax - height * 1.04f);
            float width = Mathf.Min(viewport.width * .42f, Mathf.Max(0, count - 1) * height * .43f);
            var rotation = camera.transform.rotation * Quaternion.Euler(0, 180, 0);
            while (backs.Count < count)
            {
                var card = Instantiate(cardPrefab);
                card.name = upper ? "Opponent hand back" : "Spectator hand back";
                card.ClearDefinition(); card.UpdatePowerLabel(false);
                // Anonymous backs accept hover only. No identity, definition, preview or commands.
                foreach (var collider in card.GetComponentsInChildren<Collider>()) collider.enabled = upper;
                var pointer = card.GetComponent<BattleCardPointer>();
                pointer.CanPreview = false; pointer.CanDrag = false; pointer.enabled = upper;
                var deck = zones?.FirstOrDefault(z => z.Kind == CardZoneKind.Deck && z.OwnerId == owner);
                if (deck != null) card.MoveTo(deck.CardAnchor.position, rotation, Vector3.one, immediate: true);
                backs.Add(card);
            }
            float y = upper ? viewport.yMax - height * .28f : viewport.yMin + height * .15f;
            float modelHeight = cardPrefab.GetComponent<BoxCollider>().size.y;
            for (int i = 0; i < backs.Count; i++)
            {
                float t = count <= 1 ? 0 : i / (float)(count - 1) - .5f;
                float x = viewport.center.x + t * width;
                float arc = height * .16f * (1 - 4 * t * t) * (upper ? -1 : 1);
                var facing = rotation * Quaternion.Euler(0, 0, HandFanLayout.Angle(i, count));
                float depth = 4f - .3f * (i / (float)Mathf.Max(1, count));
                var target = camera.ScreenToWorldPoint(new Vector3(x, y + arc, depth));
                float size = Vector3.Distance(camera.ScreenToWorldPoint(new Vector3(x, y + arc + height, depth)), target) / modelHeight;
                backs[i].MoveTo(target, facing, Vector3.one * size);
            }
        }

        private void ClearHiddenHands()
        {
            foreach (var card in opponentHandBacks) if (card != null) Destroy(card.gameObject);
            foreach (var card in spectatorHandBacks) if (card != null) Destroy(card.gameObject);
            opponentHandBacks.Clear(); spectatorHandBacks.Clear();
            if (playerHud != null) playerHud.SetOpponentHandHovered(false);
        }
    }
}
