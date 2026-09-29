using System;
using System.Linq;
using Mishi.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    // All controls and card slots are authored in OpeningMatchUI.prefab.
    public sealed class OpeningMatchView : MonoBehaviour
    {
        [SerializeField] private GameObject panel, gesturePanel, handPanel;
        [SerializeField] private TMP_Text title, hint, ownLabel, revealLabel;
        [SerializeField] private Button[] gestures;
        [SerializeField] private Button first, second, keep, redraw, ownPrevious, ownNext, revealPrevious, revealNext;
        [SerializeField] private BattleZoneCardItem[] ownSlots, revealSlots;
        private NetworkOpeningState state;
        private BattleCardSnapshot database;
        private NetworkCardInfo[] own = Array.Empty<NetworkCardInfo>(), revealed = Array.Empty<NetworkCardInfo>();
        private int ownPage, revealPage, submittedEpoch = -1;
        private string ownKey, revealKey, sessionId;
        private static readonly string[] GestureNames = { "石头", "剪刀", "布" };
        public bool IsOpen => panel.activeSelf;
        public event Action<int> Chosen;
        private OpeningGestureTable table;
        private void Awake()
        {
            table = GetComponent<OpeningGestureTable>();
            table.Chosen += Choose;
            for (int i = 0; i < gestures.Length; i++) { int choice = i; gestures[i].onClick.AddListener(() => Choose(choice)); }
            first.onClick.AddListener(() => Choose(0)); second.onClick.AddListener(() => Choose(1));
            keep.onClick.AddListener(() => Choose(0)); redraw.onClick.AddListener(() => Choose(1));
            ownPrevious.onClick.AddListener(() => { ownPage--; RefreshCards(false); });
            ownNext.onClick.AddListener(() => { ownPage++; RefreshCards(false); });
            revealPrevious.onClick.AddListener(() => { revealPage--; RefreshCards(true); });
            revealNext.onClick.AddListener(() => { revealPage++; RefreshCards(true); });
            Hide();
        }
        private void Choose(int value)
        {
            if (submittedEpoch == state.Epoch) return;
            submittedEpoch = state.Epoch;
            foreach (var button in gestures.Concat(new[] { first, second, keep, redraw })) button.interactable = false;
            hint.text = "已选择，等待对方…";
            Chosen?.Invoke(value);
        }
        private static string Name(string[] names, int player) => names != null && player >= 0 && player < names.Length ? names[player] : "P" + (player + 1);
        public void Apply(NetworkOpeningState next, BattleCardSnapshot cards, string[] names)
        {
            if (sessionId != next.SessionId) { submittedEpoch = -1; sessionId = next.SessionId; ownKey = revealKey = null; }
            state = next; database = cards;
            var stage = (OpeningStage)state.Stage;
            if (stage == OpeningStage.None || stage == OpeningStage.Complete) { Hide(); return; }
            panel.SetActive(true);
            bool hand = stage == OpeningStage.Hand;
            handPanel.SetActive(hand); gesturePanel.SetActive(false);
            panel.GetComponent<Image>().enabled = hand;
            table.Apply(state);
            bool canChoose = state.You >= 0 && submittedEpoch != state.Epoch;
            string seconds = $"剩余 {Mathf.CeilToInt(state.Seconds)} 秒";
            title.text = stage == OpeningStage.Gesture ? $"石头剪刀布 · 第 {state.Round} 轮" :
                stage == OpeningStage.Hand ? "确认初始手牌" : stage == OpeningStage.Result ? "出拳结果" : "选择先后手";
            for (int i = 0; i < gestures.Length; i++)
            {
                gestures[i].interactable = stage == OpeningStage.Gesture && canChoose && !state.Selected[state.You];
                var colors = gestures[i].colors;
                colors.normalColor = state.You >= 0 && state.Gestures[state.You] == i ? new Color(.38f, .69f, .94f) : Color.white;
                colors.disabledColor = state.You >= 0 && state.Gestures[state.You] == i ? new Color(.38f, .69f, .94f) : new Color(.5f, .52f, .56f, .65f);
                gestures[i].colors = colors;
            }
            first.gameObject.SetActive(stage == OpeningStage.TurnOrder); second.gameObject.SetActive(stage == OpeningStage.TurnOrder);
            first.interactable = second.interactable = canChoose && state.You == state.Winner;
            keep.gameObject.SetActive(hand && state.You >= 0 && !state.HandDecided && (state.OwnHand?.Length ?? 0) > 0);
            redraw.gameObject.SetActive(keep.gameObject.activeSelf);
            keep.interactable = canChoose; redraw.interactable = canChoose && state.CanRedraw;
            if (stage == OpeningStage.Gesture)
                hint.text = (state.You < 0 ? "等待双方选择" : state.Selected[state.You] || submittedEpoch == state.Epoch ? "已锁定，等待对方选择" : "单击一张卡出拳 · 超时自动随机选择") + " · " + seconds;
            else if (stage == OpeningStage.Result || stage == OpeningStage.TurnOrder)
                hint.text = $"{Name(names, 0)}：{GestureNames[state.Gestures[0]]}    {Name(names, 1)}：{GestureNames[state.Gestures[1]]}\n" +
                    (state.Winner < 0 ? "平局，即将重新选择" : Name(names, state.Winner) + " 获胜，选择先攻或后攻（超时默认先攻）") + " · " + seconds;
            else hint.text = (state.You < 0 ? "等待双方确认，公开的重抽手牌显示在下方" :
                state.HandDecided ? "已确认，等待对方或公开展示结束" : "没有时间 ≤ 4 的时魔，可公开全部手牌重抽一次；超时保留") + " · " + seconds;
            if (hand)
            {
                UpdateCards(state.OwnHand, false);
                UpdateCards(state.Revealed, true);
            }
        }
        private void UpdateCards(NetworkCardInfo[] values, bool publicCards)
        {
            values ??= Array.Empty<NetworkCardInfo>();
            string key = string.Join("|", values.Select(c => c.InstanceId + ":" + c.DefinitionId));
            if (key == (publicCards ? revealKey : ownKey)) return;
            if (publicCards) { revealKey = key; revealed = values; revealPage = 0; }
            else { ownKey = key; own = values; ownPage = 0; }
            RefreshCards(publicCards);
        }
        private void RefreshCards(bool publicCards)
        {
            var values = publicCards ? revealed : own;
            var slots = publicCards ? revealSlots : ownSlots;
            int pages = Math.Max(1, (values.Length + slots.Length - 1) / slots.Length);
            int page = Mathf.Clamp(publicCards ? revealPage : ownPage, 0, pages - 1);
            if (publicCards) revealPage = page; else ownPage = page;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = page * slots.Length + i;
                slots[i].Clear(); slots[i].gameObject.SetActive(index < values.Length);
                if (index < values.Length && database.Cards.TryGetValue(values[index].DefinitionId, out var definition)) slots[i].Bind(values[index], definition);
            }
            (publicCards ? revealLabel : ownLabel).text = (publicCards ? "公开的重抽前手牌" : "我的初始手牌") + $" · {values.Length} 张 · {page + 1}/{pages}";
            (publicCards ? revealPrevious : ownPrevious).interactable = page > 0;
            (publicCards ? revealNext : ownNext).interactable = page + 1 < pages;
        }
        public void Hide()
        {
            panel.SetActive(false);
            if (table != null) table.Clear();
            foreach (var slot in ownSlots.Concat(revealSlots)) slot.Clear();
            ownKey = revealKey = null;
        }
    }
}
