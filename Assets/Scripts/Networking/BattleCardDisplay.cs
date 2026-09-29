using System;
using System.Collections.Generic;
using System.IO;
using Mishi.Battle;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class BattleCardDisplay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup opacity;
        [SerializeField] private RectTransform cardPanel;
        [SerializeField] private RawImage artwork;
        [SerializeField] private TMP_Text caption;
        [SerializeField, Min(.1f)] private float duration = 1f;
        private readonly Queue<(CardDefinition Card, string Root, string Caption, int Sides, int Result)> queue = new Queue<(CardDefinition, string, string, int, int)>();
        private int diceSides, diceResult;
        private string diceCaption;
        private Vector2 captionMin, captionMax, captionPosition, captionSize;
        private TextAlignmentOptions captionAlignment;
        private Texture2D texture;
        private Tween animation;
        private bool playing;
        private void Awake()
        {
            opacity.alpha = 0; opacity.blocksRaycasts = false; opacity.interactable = false;
            var rect = caption.rectTransform;
            captionMin = rect.anchorMin; captionMax = rect.anchorMax; captionPosition = rect.anchoredPosition; captionSize = rect.sizeDelta;
            captionAlignment = caption.alignment;
        }
        private void LayoutCaption(bool dice)
        {
            var rect = caption.rectTransform;
            rect.anchorMin = dice ? Vector2.zero : captionMin;
            rect.anchorMax = dice ? Vector2.one : captionMax;
            rect.anchoredPosition = dice ? Vector2.zero : captionPosition;
            rect.sizeDelta = dice ? new Vector2(-24, -24) : captionSize;
            caption.alignment = dice ? TextAlignmentOptions.Center : captionAlignment;
        }
        public void Show(CardDefinition card, string contentRoot, CardPresentationKind kind, int owner)
        {
            if (card == null) return;
            string action = kind == CardPresentationKind.Decision ? "使用决策卡" : kind == CardPresentationKind.Reveal ? "公开展示" : "发动效果";
            queue.Enqueue((card, contentRoot, $"P{owner + 1} · {action}\n{card.Name}", 0, 0));
            if (!playing) PlayNext();
        }
        public void ShowDice(int sides, int result, int owner)
        {
            if (sides < 2 || sides > 100 || result < 1 || result > sides) return;
            queue.Enqueue((null, null, $"P{owner + 1} · 掷骰（{sides} 面）", sides, result));
            if (!playing) PlayNext();
        }
        private void PlayNext()
        {
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                try
                {
                    diceSides = item.Sides; diceResult = item.Result; diceCaption = item.Caption;
                    artwork.enabled = diceSides == 0;
                    LayoutCaption(diceSides > 0);
                    if (diceSides == 0)
                    {
                        texture = new Texture2D(2, 2);
                        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(item.Root, item.Card.ArtworkPath)), true))
                            throw new InvalidDataException("无法读取展示卡图：" + item.Card.Id);
                        artwork.texture = texture; caption.text = item.Caption;
                    }
                    playing = true; Animate(0);
                    animation = Tween.Custom(this, 0f, 1f, diceSides > 0 ? Mathf.Max(1.6f, duration) : duration, (view, progress) => view.Animate(progress), useUnscaledTime: true)
                        .OnComplete(this, view => view.Finish());
                    return;
                }
                catch (Exception e) { Debug.LogWarning(e.Message, this); ReleaseTexture(); }
            }
            playing = false; opacity.alpha = 0;
        }
        private void Animate(float progress)
        {
            float alpha = progress < .18f ? Mathf.SmoothStep(0, 1, progress / .18f) :
                progress > .75f ? Mathf.SmoothStep(1, 0, (progress - .75f) / .25f) : 1;
            opacity.alpha = alpha;
            cardPanel.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, alpha);
            if (diceSides > 0)
            {
                // Cosmetic sequence only. Never draw from the authoritative random stream.
                int shown = progress < .5f ? 1 + (Mathf.FloorToInt(progress * 48) + diceResult) % diceSides : diceResult;
                caption.text = diceCaption + "\n<size=240%><b>" + shown + "</b></size>";
                cardPanel.localRotation = progress < .5f ? Quaternion.Euler(0, 0, Mathf.Sin(progress * 100) * 5) : Quaternion.identity;
            }
            else cardPanel.localRotation = Quaternion.identity;
        }
        private void Finish() { opacity.alpha = 0; playing = false; ReleaseTexture(); PlayNext(); }
        private void ReleaseTexture()
        {
            if (artwork != null) artwork.texture = null;
            if (texture != null) Destroy(texture);
            texture = null;
        }
        public void Clear()
        {
            if (animation.isAlive) animation.Stop();
            queue.Clear(); playing = false;
            if (opacity != null) opacity.alpha = 0;
            if (cardPanel != null) cardPanel.localRotation = Quaternion.identity;
            ReleaseTexture();
        }
        private void OnDisable() { Clear(); }
    }
}
