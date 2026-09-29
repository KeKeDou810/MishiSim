using System;
using Mishi.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mishi.Networking
{
    // Player-facing controls, authored in BattleInteractionUI. No IMGUI or debug controls.
    public sealed class BattlePlayerHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text phase, notice, recording;
        [SerializeField] private TMP_Text opponentHandCount, spectatorHandCount;
        [SerializeField] private Button nextPhase, occupy, stay, leave;
        [SerializeField] private GameObject replayControls;
        [SerializeField] private Button play, previousFrame, nextFrame, back, forward, speed, restart;
        public event Action NextPhaseRequested, OccupyRequested, StayRequested, LeaveRequested;
        public event Action PlayRequested, PreviousFrameRequested, NextFrameRequested, BackRequested, ForwardRequested, SpeedRequested, RestartRequested;
        private static readonly string[] PhaseNames = { "回合开始", "抽卡阶段", "重构阶段", "时间重构阶段", "主要阶段", "战斗阶段", "结束阶段" };
        private Button[] replayButtons;
        private TMP_Text playLabel, speedLabel, nextPhaseLabel;
        private bool opponentHandHovered;
        public void PositionOpponentHandCount(Camera camera, float bottom)
        {
            var rect = opponentHandCount.rectTransform;
            var canvas = rect.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1;
            var screen = new Vector2(camera.pixelRect.center.x, bottom - 12 - rect.rect.height * scale * .5f);
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, uiCamera, out var local))
                rect.localPosition = new Vector3(local.x, local.y, 0);
        }
        public void SetOpponentHandHovered(bool hovered)
        {
            opponentHandHovered = hovered;
            opponentHandCount.gameObject.SetActive(hovered);
        }
        private void Awake()
        {
            replayButtons = new[] { play, previousFrame, nextFrame, back, forward, speed, restart };
            playLabel = play.GetComponentInChildren<TMP_Text>(); speedLabel = speed.GetComponentInChildren<TMP_Text>();
            nextPhaseLabel = nextPhase.GetComponentInChildren<TMP_Text>();
            nextPhase.onClick.AddListener(() => NextPhaseRequested?.Invoke());
            occupy.onClick.AddListener(() => OccupyRequested?.Invoke());
            stay.onClick.AddListener(() => StayRequested?.Invoke());
            leave.onClick.AddListener(() => LeaveRequested?.Invoke());
            play.onClick.AddListener(() => PlayRequested?.Invoke());
            previousFrame.onClick.AddListener(() => PreviousFrameRequested?.Invoke());
            nextFrame.onClick.AddListener(() => NextFrameRequested?.Invoke());
            back.onClick.AddListener(() => BackRequested?.Invoke());
            forward.onClick.AddListener(() => ForwardRequested?.Invoke());
            speed.onClick.AddListener(() => SpeedRequested?.Invoke());
            restart.onClick.AddListener(() => RestartRequested?.Invoke());
            occupy.gameObject.SetActive(false); stay.gameObject.SetActive(false); replayControls.SetActive(false);
            nextPhase.interactable = false;
            opponentHandCount.gameObject.SetActive(false);
        }
        public static bool CanAdvance(NetworkSnapshot state, bool blocked)
        {
            return !blocked && !state.Spectator && state.Winner < 0 && state.ActivePlayer == state.You &&
                state.ChoicePlayer < 0 && state.ResponsePlayer < 0 && state.OccupationNode < 0 &&
                ((TestTurnPhase)state.Phase == TestTurnPhase.Main || (TestTurnPhase)state.Phase == TestTurnPhase.Combat);
        }
        public void Apply(NetworkSnapshot? snapshot, bool blocked, bool canLeave, string message, string recordingMessage,
            bool replay, bool playing, double position, double duration, float rate)
        {
            notice.text = message ?? ""; recording.text = recordingMessage ?? "";
            leave.interactable = canLeave;
            replayControls.SetActive(replay);
            foreach (var button in replayButtons) button.interactable = !blocked;
            if (replay)
            {
                playLabel.text = playing ? "暂停" : "播放";
                speedLabel.text = rate + "×";
                recording.text = $"录像  {position:0.0} / {duration:0.0} 秒";
            }
            bool occupation = snapshot.HasValue && !snapshot.Value.Spectator && snapshot.Value.Winner < 0 &&
                snapshot.Value.ActivePlayer == snapshot.Value.You && snapshot.Value.OccupationNode >= 0;
            occupy.gameObject.SetActive(occupation && !replay); stay.gameObject.SetActive(occupation && !replay);
            occupy.interactable = stay.interactable = !blocked && snapshot.HasValue && snapshot.Value.ChoicePlayer < 0 && snapshot.Value.ResponsePlayer < 0;
            nextPhase.gameObject.SetActive(!replay && !occupation);
            nextPhase.interactable = snapshot.HasValue && CanAdvance(snapshot.Value, blocked);
            opponentHandCount.gameObject.SetActive(snapshot.HasValue && opponentHandHovered);
            spectatorHandCount.gameObject.SetActive(snapshot.HasValue && snapshot.Value.Spectator);
            if (!snapshot.HasValue) { phase.text = "等待对局"; nextPhaseLabel.text = "下一阶段"; return; }
            var state = snapshot.Value;
            opponentHandCount.text = $"{(state.Spectator ? "P" + (2 - state.You) : "对方")}手牌 · {BattleNetworkSession.HiddenHandCount(state, 1 - state.You)}";
            spectatorHandCount.text = $"P{state.You + 1} 手牌 · {BattleNetworkSession.HiddenHandCount(state, state.You)}";
            string owner = state.Spectator ? "P" + (state.ActivePlayer + 1) : state.ActivePlayer == state.You ? "我方" : "对方";
            string current = state.Phase >= 0 && state.Phase < PhaseNames.Length ? PhaseNames[state.Phase] : "阶段处理中";
            phase.text = state.Winner >= 0 ? "对局结束" : $"第 {state.Turn} 回合 · {owner}\n{current}";
            nextPhaseLabel.text = (TestTurnPhase)state.Phase == TestTurnPhase.Combat ? "结束战斗阶段" :
                (TestTurnPhase)state.Phase == TestTurnPhase.Main ? "结束主要阶段" : "下一阶段";
        }
    }
}
