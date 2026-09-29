using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class BattleZoneCardItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IScrollHandler
    {
        [SerializeField] private RawImage artwork;
        [SerializeField] private TMP_Text label;
        private CardDefinition definition;
        private Texture2D texture;
        private string instanceId;
        private int power, time;
        public event Action<string> Selected;
        public void SetCaption(string caption) { label.text = caption; }
        public void Bind(NetworkCardInfo info, CardDefinition card)
        {
            Clear(); gameObject.SetActive(true);
            if (info.FaceDown)
            { if (info.HiddenChoice) instanceId = info.InstanceId; label.text = "背面卡片"; artwork.color = AtomOneTheme.Surface; return; }
            definition = card;
            power = info.Power; time = info.Time;
            instanceId = info.InstanceId;
            label.text = (info.InstanceId.StartsWith("name:", StringComparison.Ordinal) ? "" : "P" + (info.Owner + 1) + " · ") + card.Name + "\n<size=70%>" + card.Id +
                (info.Covered ? " · 超频下层" : "") + ((Mishi.Battle.TestCardZone)info.Zone == Mishi.Battle.TestCardZone.Exile ? " · 除外" : "") + "\n时间 " + info.Time + (card.IsDecision || card.IsPlayer ? "" : " · 力量 " + info.Power) + "</size>";
            try
            {
                texture = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(CardDatabaseService.Instance.ContentRoot, card.ArtworkPath)), true))
                    throw new InvalidDataException("无法读取卡图：" + card.ArtworkPath);
                artwork.texture = texture; artwork.color = Color.white;
            }
            catch (Exception e) { Debug.LogWarning(e.Message, this); artwork.color = AtomOneTheme.Raised; }
        }
        public void Clear()
        {
            definition = null; instanceId = null;
            if (artwork != null) artwork.texture = null;
            if (texture != null) Destroy(texture);
            texture = null;
        }
        private void Preview()
        { if (definition != null) CardPreviewRequestEvent.Trigger(definition, CardDatabaseService.Instance.ContentRoot, power, time); }
        public void OnPointerEnter(PointerEventData data) { if (!data.dragging) Preview(); }
        public void OnScroll(PointerEventData data)
        {
            if (definition == null || data.dragging) return;
            CardPreviewScrollEvent.Trigger(data.scrollDelta.y);
            data.Use();
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || data.dragging || !data.eligibleForClick || string.IsNullOrEmpty(instanceId)) return;
            Preview(); Selected?.Invoke(instanceId);
        }
        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}
