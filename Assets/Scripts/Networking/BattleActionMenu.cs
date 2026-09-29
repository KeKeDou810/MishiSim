using System;
using Mishi.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    // All panels/buttons are authored in the scene/prefab. Runtime only binds events/data.
    public sealed class BattleActionMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button summon, overclock, move, attack, decision, inspect, close, activate;
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private TMP_Text targetHint;
        [SerializeField] private Button cancelTarget;
        public event Action<TestCommandKind> CommandSelected;
        public event Action InspectRequested, Cancelled;
        private Action confirmation;
        private void Awake()
        {
            activate.onClick.AddListener(() => Choose(TestCommandKind.ActivateEffect));
            summon.onClick.AddListener(() => Choose(TestCommandKind.Summon));
            overclock.onClick.AddListener(() => Choose(TestCommandKind.Overclock));
            move.onClick.AddListener(() => Choose(TestCommandKind.MoveContract));
            attack.onClick.AddListener(() => Choose(TestCommandKind.Attack));
            decision.onClick.AddListener(() => Choose(TestCommandKind.PlayDecision));
            inspect.onClick.AddListener(() => { panel.SetActive(false); InspectRequested?.Invoke(); });
            close.onClick.AddListener(Cancel);
            cancelTarget.onClick.AddListener(Cancel);
            Hide();
        }
        private void Choose(TestCommandKind kind)
        {
            if (confirmation != null)
            {
                var submit = confirmation;
                Hide();
                submit();
                return;
            }
            panel.SetActive(false); CommandSelected?.Invoke(kind);
        }
        public void ConfirmAction(string description, Action submit)
        {
            Show(description, true, false, false, false, false, false);
            confirmation = submit;
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 260);
            title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 116);
            title.rectTransform.anchoredPosition = new Vector2(0, 58);
            title.enableAutoSizing = true; title.fontSizeMin = 16; title.fontSizeMax = 22;
            ((RectTransform)summon.transform).anchoredPosition = new Vector2(0, -34);
            ((RectTransform)close.transform).anchoredPosition = new Vector2(0, -82);
            summon.GetComponentInChildren<TMP_Text>(true).text = "确认执行";
            close.GetComponentInChildren<TMP_Text>(true).text = "取消";
        }
        public bool IsOpen => panel.activeSelf || targetPanel.activeSelf;
        private void Cancel() { Hide(); Cancelled?.Invoke(); }
        public void Show(string cardName, bool canSummon, bool canOverclock, bool canMove, bool canAttack, bool canDecision, bool canInspect, bool canActivate = false)
        {
            confirmation = null;
            summon.GetComponentInChildren<TMP_Text>(true).text = "登场";
            close.GetComponentInChildren<TMP_Text>(true).text = "关闭";
            title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 36);
            targetPanel.SetActive(false); panel.SetActive(true); title.text = cardName;
            var buttons = new[] { summon, overclock, move, attack, decision, activate, inspect, close };
            var visible = new[] { canSummon, canOverclock, canMove, canAttack, canDecision, canActivate, canInspect, true };
            int count = 0;
            for (int i = 0; i < buttons.Length; i++) if (visible[i]) count++;
            float height = 64 + count * 44;
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            title.rectTransform.anchoredPosition = new Vector2(0, height / 2 - 28);
            int row = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(visible[i]);
                buttons[i].interactable = visible[i];
                if (visible[i]) ((RectTransform)buttons[i].transform).anchoredPosition = new Vector2(0, height / 2 - 72 - row++ * 44);
            }
        }
        public void PickTarget(string command)
        { panel.SetActive(false); targetPanel.SetActive(true); targetHint.text = command + "：请选择目标圆阵"; }
        public void PickEffectZone(string prompt)
        { panel.SetActive(false); targetPanel.SetActive(true); targetHint.text = prompt + "：点击场外区"; cancelTarget.gameObject.SetActive(false); }
        public void Hide() { confirmation = null; cancelTarget.gameObject.SetActive(true); panel.SetActive(false); targetPanel.SetActive(false); }
    }
}
