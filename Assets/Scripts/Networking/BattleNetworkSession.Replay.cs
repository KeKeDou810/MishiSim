using System;
using System.Linq;
using FishNet.Transporting;
using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private BattleReplay replay;
        private int replayNext, replayFrameIndex;
        private double replayPosition, replayTimerAt;
        private bool replayPlaying;
        private float replaySpeed = 1;
        private NetworkTurnTimer replayTimer;
        private void StartReplay(string path)
        {
            try
            {
                replay = BattleReplay.Load(path);
                database = CardDatabaseService.Instance;
                if (!database.ReloadDatabase()) throw new InvalidOperationException(database.LastError);
                if (!database.Modes.Any(mode => mode.Id == replay.Header.ModeId)) throw new InvalidOperationException("录像使用的游戏模式不存在。");
                catalog = database.BeginBattle(); ownsDatabaseLock = true;
                if (ComputeSessionHash(replay.Header.ModeId) != replay.Header.ContentHash) throw new InvalidOperationException("录像与当前卡库、规则或场地版本不一致，请使用录制时的 Content 和游戏版本。");
                spectator = true; sessionOpen = true; stopping = false; connectDeadline = 0;
                clockHud.ManualTime = true;
                replayPlaying = true; replaySpeed = 1;
                SeekReplay(0);
            }
            catch (Exception e) { replay = null; status = "无法打开录像：" + e.Message; StopSession(); }
        }
        private void ApplyReplayEntry(int index, bool animate)
        {
            var entry = replay.Entries[index];
            if (entry.Kind == "frame")
            {
                replayFrameIndex = index;
                OnSnapshot(entry.Frame.State, Channel.Reliable);
                var state = entry.Frame.State;
                replayTimer = new NetworkTurnTimer { RemainingSeconds = state.RemainingTurnSeconds, Running = state.TimerRunning,
                    ResponsePlayer = state.ChoicePlayer >= 0 ? state.ChoicePlayer : state.ResponsePlayer, RemainingResponseSeconds = state.RemainingResponseSeconds };
                replayTimerAt = entry.Seconds;
                if (animate) foreach (var item in entry.Frame.Presentations ?? Array.Empty<NetworkCardPresentation>()) OnCardPresentation(item, Channel.Reliable);
            }
            else if (entry.Kind == "timer") { replayTimer = entry.Timer; replayTimerAt = entry.Seconds; }
        }
        private void SeekReplay(double seconds)
        {
            if (replay == null || !sessionOpen) return;
            replayPosition = Math.Max(0, Math.Min(replay.Duration, seconds));
            int frame = replay.FrameAt(replayPosition);
            ResetReplayView();
            ApplyReplayEntry(frame, false);
            replayNext = frame + 1;
            while (replayNext < replay.Entries.Count && replay.Entries[replayNext].Seconds <= replayPosition) ApplyReplayEntry(replayNext++, false);
            RefreshReplayTimer();
        }
        private void ResetReplayView()
        {
            CancelInteraction(); snapshot = null; lastPresentation = receivedClockSequence = 0;
            ClearBattlePreview(); cardDisplay.Clear(); zoneWindow.Hide();
        }
        private void StepReplay(int direction)
        {
            replayPlaying = false;
            for (int i = replayFrameIndex + direction; i >= 0 && i < replay.Entries.Count; i += direction)
            {
                if (replay.Entries[i].Kind != "frame") continue;
                ResetReplayView(); replayPosition = replay.Entries[i].Seconds;
                ApplyReplayEntry(i, false); replayNext = i + 1; RefreshReplayTimer(); break;
            }
        }
        private void UpdateReplay()
        {
            if (replay == null || !sessionOpen) return;
            if (replayPlaying)
            {
                replayPosition = Math.Min(replay.Duration, replayPosition + Time.unscaledDeltaTime * replaySpeed);
                while (replayNext < replay.Entries.Count && replay.Entries[replayNext].Seconds <= replayPosition)
                    ApplyReplayEntry(replayNext++, true);
                if (replayNext >= replay.Entries.Count) replayPlaying = false;
            }
            RefreshReplayTimer();
        }
        private void RefreshReplayTimer()
        {
            float elapsed = (float)Math.Max(0, replayPosition - replayTimerAt);
            clockHud.ApplyTimer(replayTimer.RemainingSeconds - (replayTimer.Running && replayTimer.ResponsePlayer < 0 ? elapsed : 0),
                replayTimer.Running, replayTimer.ResponsePlayer, replayTimer.RemainingResponseSeconds - elapsed);
        }

    }
}
