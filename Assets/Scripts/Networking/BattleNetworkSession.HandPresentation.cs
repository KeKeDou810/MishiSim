using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private int handWindowStart;
        private bool panningHand;
        private float handPanX, handPanRemainder;
        private void StartHandPan()
        {
            if (InteractionOpen || Mouse.current == null) return;
            panningHand = true; handPanX = Mouse.current.position.ReadValue().x; handPanRemainder = 0;
            ClearBattlePreview();
        }
        private void UpdateHandPan(int total, int capacity)
        {
            if (!panningHand) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || InteractionOpen)
            { panningHand = false; return; }
            float x = mouse.position.ReadValue().x;
            handPanRemainder += handPanX - x; handPanX = x;
            const float pixelsPerCard = 32;
            int steps = (int)(handPanRemainder / pixelsPerCard);
            if (steps == 0) return;
            handWindowStart = Mathf.Clamp(handWindowStart + steps, 0, Mathf.Max(0, total - capacity));
            handPanRemainder -= steps * pixelsPerCard;
        }
        private CardPreview[] handPreviews;
        private readonly Vector3[] previewCorners = new Vector3[4];
        private Guid hoveredHand;
        private Rect lastHandArea;
        private Rect HandArea(Camera camera)
        {
            Rect viewport = camera.pixelRect, safe = Screen.safeArea;
            float left = Mathf.Max(viewport.xMin, safe.xMin) + 16, right = Mathf.Min(viewport.xMax, safe.xMax) - 16;
            if (handPreviews == null) handPreviews = FindObjectsByType<CardPreview>(FindObjectsSortMode.None);
            foreach (var preview in handPreviews)
            {
                if (preview == null || !preview.isActiveAndEnabled) continue;
                // Floating previews sit above the hand. Never change its width on open/close.
                if (preview.GetComponentInParent<BattleCardPreviewOverlay>() != null) continue;
                var rect = preview.GetComponentInParent<RectTransform>();
                if (rect == null) continue;
                var canvas = rect.GetComponentInParent<Canvas>();
                Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                rect.GetWorldCorners(previewCorners);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var corner in previewCorners)
                {
                    float x = RectTransformUtility.WorldToScreenPoint(uiCamera, corner).x;
                    min = Mathf.Min(min, x); max = Mathf.Max(max, x);
                }
                if ((min + max) * .5f < viewport.center.x) left = Mathf.Max(left, max + 20);
                else right = Mathf.Min(right, min - 20);
            }
            return Rect.MinMaxRect(left, Mathf.Max(viewport.yMin, safe.yMin) + 8, Mathf.Max(left, right), Mathf.Min(viewport.yMax, safe.yMax));
        }
        private void ScrollHand(float delta)
        {
            if (!snapshot.HasValue || Camera.main == null || Mathf.Abs(delta) < .01f) return;
            foreach (var card in visuals.Values) if (card != null && card.GetComponent<BattleCardPointer>().IsDragging) return;
            int capacity = HandFanLayout.Capacity(HandArea(Camera.main));
            handWindowStart = Mathf.Clamp(handWindowStart + (delta < 0 ? 1 : -1), 0, Mathf.Max(0, snapshot.Value.OwnHand.Length - capacity));
        }
        private void LayoutHandFan()
        {
            if (!snapshot.HasValue || Camera.main == null) return;
            var hand = snapshot.Value.OwnHand;
            var camera = Camera.main;
            Rect area = HandArea(camera);
            bool resized = area != lastHandArea; lastHandArea = area;
            int capacity = HandFanLayout.Capacity(area), count = Mathf.Min(hand.Length, capacity);
            UpdateHandPan(hand.Length, capacity);
            handWindowStart = Mathf.Clamp(handWindowStart, 0, Mathf.Max(0, hand.Length - count));
            float height = HandFanLayout.CardHeight(area);
            // Stable resting-card acquisition prevents tweening geometry from switching neighbours.
            for (int i = 0; i < hand.Length; i++)
            {
                if (!visuals.TryGetValue(Guid.Parse(hand[i].InstanceId), out var card)) continue;
                var pointer = card.GetComponent<BattleCardPointer>();
                if (pointer.IsDragging) continue;
                int slot = i - handWindowStart;
                bool visible = area.width > 1 && slot >= 0 && slot < count;
                if (!visible) { pointer.ClearHandArea(); card.gameObject.SetActive(false); continue; }
                bool entering = !pointer.IsHandManaged || !card.gameObject.activeSelf;
                card.gameObject.SetActive(true);
                pointer.SetHandArea(camera, HandFanLayout.HitRect(area, slot, count));
                pointer.CanPanHand = hand.Length > capacity;
                pointer.SetHandShape(HandFanLayout.Center(area, slot, count), new Vector2(height * .7154f, height), HandFanLayout.Angle(slot, count));
                if (resized) pointer.SnapHandLayout = true;
            }
            var hover = !panningHand && !InteractionOpen && Mouse.current != null ? BattleCardPointer.HandAt(camera, Mouse.current.position.ReadValue()) : null;
            hoveredHand = hover != null ? hover.GetComponent<BattleCard>().InstanceId : Guid.Empty;
            float blend = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
            for (int i = handWindowStart; i < handWindowStart + count; i++)
            {
                if (!visuals.TryGetValue(Guid.Parse(hand[i].InstanceId), out var card) || !card.gameObject.activeSelf) continue;
                var pointer = card.GetComponent<BattleCardPointer>();
                if (pointer.IsDragging) continue;
                int slot = i - handWindowStart;
                bool raised = card.InstanceId == hoveredHand;
                pointer.SetHandRaised(raised);
                var center = raised ? HandFanLayout.RaisedCenter(area, slot, count) : HandFanLayout.Center(area, slot, count);
                float depth = raised ? 3.5f : 4f - slot * .004f;
                var target = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, depth));
                var rotation = camera.transform.rotation * Quaternion.Euler(0, 0, raised ? 0 : HandFanLayout.Angle(slot, count));
                float pixels = height * (raised ? 1.16f : 1);
                float worldHeight = Vector3.Distance(camera.ScreenToWorldPoint(new Vector3(center.x, center.y + pixels, depth)), target);
                var box = cardPrefab.GetComponent<BoxCollider>();
                float scale = worldHeight / (box != null ? box.size.y : .866f);
                card.MoveTo(target, rotation, Vector3.one * scale, immediate: pointer.SnapHandLayout);
                pointer.SnapHandLayout = false;
            }
        }
    }
}
