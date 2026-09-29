using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    public sealed class BattleClockHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text own;
        [SerializeField] private TMP_Text opponent;
        [SerializeField] private TMP_Text result;
        [SerializeField] private TMP_Text ownTimer, opponentTimer;
        [SerializeField] private Button passResponse, exile, resumeChoice;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("previewWindow")] private Button logWindow;
        public event System.Action ResponsePassed, ExileRequested, ChoiceRequested, LogRequested;
        private int you, activePlayer, winner = -1, responsePlayer = -1;
        private double receivedAt;
        private float turnSeconds, responseSeconds;
        private bool running, hasState;
        public bool ManualTime { get; set; }
        public void SetChatUnread(int count)
        {
            var label = logWindow.GetComponentInChildren<TMP_Text>();
            label.text = count > 0 ? $"新消息 ({count})" : "操作记录";
            label.color = count > 0 ? new Color(.898f, .753f, .482f) : new Color(.67f, .698f, .749f);
        }
        private string connectionMessage;
        public void SetConnectionStatus(string message)
        {
            connectionMessage = message;
            if (!string.IsNullOrEmpty(message)) { ownTimer.text = opponentTimer.text = message; passResponse.interactable = resumeChoice.interactable = false; }
            else { passResponse.interactable = resumeChoice.interactable = true; if (hasState) RefreshTimers(); }
        }
        private string[] playerNames;
        public void SetNames(string[] names) { playerNames = names; own.richText = opponent.richText = false; }
        private string Label(int player, bool spectator, bool local) => playerNames != null && player < playerNames.Length
            ? playerNames[player] : spectator ? "P" + (player + 1) : local ? "我方" : "对方";
        private void Awake()
        {
            logWindow.onClick.AddListener(() => LogRequested?.Invoke());
            resumeChoice.onClick.AddListener(() => ChoiceRequested?.Invoke());
            resumeChoice.gameObject.SetActive(false);
            exile.onClick.AddListener(() => ExileRequested?.Invoke());
            passResponse.onClick.AddListener(() => ResponsePassed?.Invoke());
            passResponse.gameObject.SetActive(false);
        }

        public void Apply(NetworkSnapshot state)
        {
            int you = state.You, other = 1 - you;
            own.text = $"{Label(you, state.Spectator, true)}\n（{(state.ClockKinds[you] == 1 ? "白时钟" : "黑时钟")}）\n费用指针 {state.CostPointers[you]}\n伤害指针 {state.DamagePointers[you]}";
            opponent.text = $"{Label(other, state.Spectator, false)}\n（{(state.ClockKinds[other] == 1 ? "白时钟" : "黑时钟")}）\n费用指针 {state.CostPointers[other]}\n伤害指针 {state.DamagePointers[other]}";
            result.text = state.Winner == 2 ? state.ResultReason : state.Winner < 0 ? "" : (state.Spectator ? "P" + (state.Winner + 1) + " 胜利" : state.Winner == you ? "胜利" : "败北") + "\n" + state.ResultReason;
            this.you = you; activePlayer = state.ActivePlayer; winner = state.Winner; hasState = true;
            passResponse.GetComponentInChildren<TMP_Text>(true).text = state.ChoicePlayer >= 0 ? "跳过选择" : "放弃响应";
            passResponse.gameObject.SetActive(!state.Spectator && winner < 0 && (state.ChoicePlayer >= 0 ? state.ChoicePlayer == you && state.ChoiceOptional : state.ResponsePlayer == you));
            resumeChoice.gameObject.SetActive(!state.Spectator && winner < 0 && state.ChoicePlayer == you);
            ApplyTimer(state.RemainingTurnSeconds, state.TimerRunning, state.ChoicePlayer >= 0 ? state.ChoicePlayer : state.ResponsePlayer, state.RemainingResponseSeconds);
        }
        public void ApplyTimer(float turnRemaining, bool isRunning, int responseOwner, float responseRemaining)
        {
            turnSeconds = turnRemaining; running = isRunning; responsePlayer = responseOwner; responseSeconds = responseRemaining;
            receivedAt = Time.realtimeSinceStartupAsDouble;
            RefreshTimers();
        }
        public void ClearTimer()
        {
            hasState = false; connectionMessage = null;
            resumeChoice.gameObject.SetActive(false);
            passResponse.gameObject.SetActive(false);
            ownTimer.text = opponentTimer.text = "等待对局";
        }
        private void Update() { if (hasState) RefreshTimers(); }
        private void RefreshTimers()
        {
            if (!string.IsNullOrEmpty(connectionMessage)) { ownTimer.text = opponentTimer.text = connectionMessage; return; }
            double elapsed = ManualTime ? 0 : Time.realtimeSinceStartupAsDouble - receivedAt;
            for (int player = 0; player < 2; player++)
            {
                string text;
                if (winner >= 0) text = "对战结束";
                else if (responsePlayer >= 0) text = player == responsePlayer ? "响应 " + Format(responseSeconds - elapsed) : "等待响应";
                else if (!running) text = "准备中";
                else text = player == activePlayer ? "回合 " + Format(turnSeconds - elapsed) : "等待回合";
                (player == you ? ownTimer : opponentTimer).text = text;
            }
        }
        private static string Format(double seconds)
        {
            int value = Mathf.CeilToInt((float)System.Math.Max(0, seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }
    }
}

