using System;
using Mishi.Battle;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private BattlePlayerHud playerHud;
        private void BindPlayerHud()
        {
            playerHud = actionMenu.GetComponent<BattlePlayerHud>();
            if (playerHud == null) throw new InvalidOperationException("战斗 UI 缺少玩家阶段面板。");
            playerHud.NextPhaseRequested += () => {
                if (snapshot.HasValue && BattlePlayerHud.CanAdvance(snapshot.Value, pending || confirmingAction || InteractionOpen))
                    SendCommand(TestCommandKind.NextPhase, Guid.Empty, -1);
            };
            playerHud.OccupyRequested += () => SendCommand(TestCommandKind.Occupy, Guid.Empty, -1);
            playerHud.StayRequested += () => SendCommand(TestCommandKind.Stay, Guid.Empty, -1);
            playerHud.LeaveRequested += LeaveBattle;
            playerHud.PlayRequested += () => replayPlaying = !replayPlaying;
            playerHud.PreviousFrameRequested += () => StepReplay(-1);
            playerHud.NextFrameRequested += () => StepReplay(1);
            playerHud.BackRequested += () => SeekReplay(replayPosition - 10);
            playerHud.ForwardRequested += () => SeekReplay(replayPosition + 10);
            playerHud.SpeedRequested += () => replaySpeed = replaySpeed >= 4 ? .5f : replaySpeed * 2;
            playerHud.RestartRequested += () => { SeekReplay(0); replayPlaying = true; };
        }
        private void RefreshPlayerHud()
        {
            if (playerHud == null) return;
            string recordingText = !string.IsNullOrEmpty(recordingError) ? recordingError :
                recording != null && !recording.Finished ? "自动录像中 · 公开信息" : recordingOutcome;
            if (spectator && replay == null) recordingText = "观战 · " + recordingText;
            playerHud.Apply(snapshot, pending || confirmingAction || InteractionOpen,
                recordingPanel == null || !recordingPanel.IsOpen, status, recordingText,
                replay != null, replayPlaying, replayPosition, replay?.Duration ?? 0, replaySpeed);
        }
    }
}
