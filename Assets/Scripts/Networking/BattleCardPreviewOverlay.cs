using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MoreMountains.Tools;

namespace Mishi.Networking
{
    // Presentation only. It never blocks the whole board or submits a game command.
    public sealed class BattleCardPreviewOverlay : MonoBehaviour, MMEventListener<CardPreviewScrollEvent>
    {
        [SerializeField] private CardPreview preview;
        [SerializeField] private CanvasGroup opacity;
        [SerializeField] private RectTransform panel;
        [SerializeField, Min(.01f)] private float duration = .2f;
        [SerializeField] private Vector2 hiddenOffset = new Vector2(-28, 0);
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private Tween animation;
        private Vector2 origin, pressPosition;
        private float visibility;
        private bool pressedOnEmptyBoard;
        private int openedFrame = -1;
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            origin = panel.anchoredPosition;
            ResetVisibility();
        }
        private void OnEnable()
        {
            preview.Shown += ShowCard;
            MMEventManager.AddListener<CardPreviewScrollEvent>(this);
        }
        private void OnDisable()
        {
            preview.Shown -= ShowCard;
            MMEventManager.RemoveListener<CardPreviewScrollEvent>(this);
            ResetVisibility();
        }
        public void OnMMEvent(CardPreviewScrollEvent e)
        {
            if (IsOpen) preview.ScrollEffect(e.Delta);
        }
        private void ShowCard()
        {
            preview.SelectPage(false);
            SetVisible(true);
        }
        public void Hide() { SetVisible(false); }
        public void Clear()
        {
            ResetVisibility();
            preview.Clear();
        }
        private void SetVisible(bool visible)
        {
            if (visible) openedFrame = Time.frameCount;
            if (IsOpen == visible) return;
            IsOpen = visible;
            opacity.interactable = visible;
            opacity.blocksRaycasts = visible;
            if (animation.isAlive) animation.Stop();
            float target = visible ? 1 : 0;
            if (!Application.isPlaying) { ApplyVisibility(target); return; }
            animation = Tween.Custom(this, visibility, target, duration,
                (view, value) => view.ApplyVisibility(value), ease: Ease.OutCubic, useUnscaledTime: true);
        }
        private void ApplyVisibility(float value)
        {
            visibility = value;
            opacity.alpha = value;
            panel.anchoredPosition = origin + hiddenOffset * (1 - value);
            panel.localScale = Vector3.one * Mathf.Lerp(.97f, 1, value);
        }
        private void ResetVisibility()
        {
            if (animation.isAlive) animation.Stop();
            IsOpen = false; pressedOnEmptyBoard = false;
            opacity.interactable = opacity.blocksRaycasts = false;
            ApplyVisibility(0);
        }
        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Hide();
            var mouse = Mouse.current;
            if (!IsOpen || mouse == null) { pressedOnEmptyBoard = false; return; }
            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressPosition = position;
                pressedOnEmptyBoard = IsEmptyBoard(position);
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            int threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10;
            if (pressedOnEmptyBoard && openedFrame != Time.frameCount &&
                Vector2.Distance(pressPosition, position) <= threshold && IsEmptyBoard(position)) Hide();
            pressedOnEmptyBoard = false;
        }
        private bool IsEmptyBoard(Vector2 position)
        {
            if (EventSystem.current == null) return true;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            if (hits.Count == 0) return true;
            var hit = hits[0].gameObject;
            // Scrolling, tabs, commands, choices and other HUD controls are not empty board.
            return hit.GetComponent<RectTransform>() == null && hit.GetComponentInParent<BattleCardPointer>() == null;
        }
    }
}
