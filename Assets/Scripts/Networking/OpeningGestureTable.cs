using System;
using Mishi.Battle;
using UnityEngine;

namespace Mishi.Networking
{
    // Presentation only: selected gestures still come from the sealed host-side opening protocol.
    public sealed class OpeningGestureTable : MonoBehaviour
    {
        [SerializeField] private OpeningGestureCard cardPrefab;
        private readonly OpeningGestureCard[] hand = new OpeningGestureCard[3], placed = new OpeningGestureCard[2];
        private Transform[] anchors;
        private NetworkOpeningState state;
        private bool active;
        private int localChoice = -1, round;
        private string session;
        public event Action<int> Chosen;
        public void Configure(Transform first, Transform second) { anchors = new[] { first, second }; }
        public void Apply(NetworkOpeningState value)
        {
            if (session != value.SessionId || round != value.Round) { Clear(); session = value.SessionId; round = value.Round; }
            state = value;
            active = value.Stage == (int)OpeningStage.Gesture || value.Stage == (int)OpeningStage.Result || value.Stage == (int)OpeningStage.TurnOrder;
            if (!active) { Clear(); return; }
            if (value.You >= 0 && value.Selected[value.You]) localChoice = value.Gestures[value.You];
            if (value.You >= 0)
                for (int i = 0; i < 3; i++)
                {
                    if (hand[i] != null) continue;
                    int choice = i;
                    hand[i] = Instantiate(cardPrefab);
                    hand[i].Pointer.Clicked += _ => {
                        if (!active || state.Stage != (int)OpeningStage.Gesture || localChoice >= 0 || state.Selected[state.You]) return;
                        localChoice = choice; Chosen?.Invoke(choice);
                    };
                }
        }
        private void LateUpdate()
        {
            if (!active || anchors == null || Camera.main == null) return;
            var camera = Camera.main;
            var area = camera.pixelRect;
            float height = Mathf.Min(230, area.height * .26f);
            bool reveal = state.Stage != (int)OpeningStage.Gesture;
            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i] == null) continue;
                bool selected = localChoice == i;
                hand[i].gameObject.SetActive(localChoice < 0 || selected);
                if (selected) { Place(hand[i], state.You, i, reveal); continue; }
                hand[i].Show(i, true, localChoice < 0 && !reveal);
                bool hovered = hand[i].Pointer.IsHovered;
                float x = area.center.x + (i - 1) * height * .52f;
                float y = area.yMin + height * (.6f + (i == 1 ? .04f : 0) + (hovered ? .14f : 0));
                float depth = hovered ? 3.5f : 4f - i * .01f;
                var pos = camera.ScreenToWorldPoint(new Vector3(x, y, depth));
                float scale = Vector3.Distance(camera.ScreenToWorldPoint(new Vector3(x, y + height, depth)), pos) / .866f;
                hand[i].Card.MoveTo(pos, camera.transform.rotation * Quaternion.Euler(0, 0, hovered ? 0 : (1 - i) * 9), Vector3.one * scale);
            }
            for (int p = 0; p < 2; p++)
            {
                if (p == state.You || !state.Selected[p]) continue;
                if (placed[p] == null)
                {
                    placed[p] = Instantiate(cardPrefab);
                    placed[p].Card.MoveTo(camera.ScreenToWorldPoint(new Vector3(area.center.x, area.yMax, 4)), camera.transform.rotation, immediate: true);
                }
                Place(placed[p], p, state.Gestures[p], reveal);
            }
        }
        private void Place(OpeningGestureCard card, int owner, int gesture, bool reveal)
        {
            card.Show(gesture, reveal, false);
            var anchor = anchors[owner];
            card.Card.MoveTo(anchor.position + Vector3.up * .035f,
                anchor.rotation * Quaternion.Euler(0, reveal ? 0 : 180, owner == 1 ? 180 : 0), Vector3.one);
        }
        public void Clear()
        {
            foreach (var card in hand) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            foreach (var card in placed) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
            Array.Clear(hand, 0, hand.Length); Array.Clear(placed, 0, placed.Length);
            localChoice = -1; active = false;
        }
        private void OnDestroy() => Clear();
    }
}
