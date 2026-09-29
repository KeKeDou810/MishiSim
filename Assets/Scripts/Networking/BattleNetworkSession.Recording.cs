using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private BattleRecording recording;
        private BattleRecordingPanel recordingPanel;
        private bool recordingDecided, leavingBattle, quitting;
        private string recordingError = "", recordingOutcome = "";
        private void BindRecordingPanel()
        {
            recordingPanel = actionMenu.GetComponent<BattleRecordingPanel>();
            if (recordingPanel == null) throw new InvalidOperationException("战斗 UI 缺少录像保存窗口，请更新 BattleInteractionUI 预制体。");
            recordingPanel.SaveRequested += SaveRecording;
            recordingPanel.DiscardRequested += () => { DiscardRecording(); AfterRecordingDecision(); };
        }
        private void RecordFrame(NetworkPublicFrame frame)
        {
            if (effectTestMode || replay != null || recordingDecided || !string.IsNullOrEmpty(recordingError)) return;
            try
            {
                if (recording == null)
                {
                    BattleRecording.CleanAbandoned(database.ContentRoot);
                    recording = new BattleRecording(database.ContentRoot, new RecordingHeader {
                        MatchId = frame.State.MatchId, ContentHash = hash, ModeId = database.ActiveMode.Id
                    });
                }
                recording.Append(frame);
                if (frame.State.Winner >= 0) FinishRecording(frame.State.ResultReason);
            }
            catch (Exception e) { RecordingFailed(e); }
        }
        private void RecordTimer(NetworkTurnTimer timer)
        {
            if (replay != null || recording == null || !string.IsNullOrEmpty(recordingError)) return;
            try { recording.Append(timer); } catch (Exception e) { RecordingFailed(e); }
        }
        private void RecordingFailed(Exception error)
        {
            recordingError = "录像写入失败：" + error.Message;
            Debug.LogWarning(recordingError, this);
            try { recording?.Dispose(); } catch (Exception cleanup) { Debug.LogWarning(cleanup.Message, this); }
            recording = null; recordingDecided = true;
        }
        private void FinishRecording(string reason)
        {
            if (recording == null || recording.Finished || recordingDecided) return;
            try
            {
                recording.Finish(reason);
                if (recording.HasFrames && !quitting)
                {
                    CancelInteraction(); recordingPanel.Show(string.IsNullOrEmpty(reason) ? "录制结束" : reason);
                }
            }
            catch (Exception e) { RecordingFailed(e); }
        }
        private void SaveRecording(string name)
        {
            try
            {
                string saved = recording.Save(name);
                recording.Dispose(); recording = null; recordingDecided = true;
                recordingOutcome = "已保存：" + Path.GetFileName(saved);
                AfterRecordingDecision();
            }
            catch (Exception e) { recordingPanel.ShowError(e.Message); }
        }
        private void DiscardRecording()
        {
            try { recording?.Dispose(); }
            catch (Exception e) { Debug.LogWarning("临时录像删除失败：" + e.Message, this); }
            recording = null; recordingDecided = true; recordingOutcome = "未保存的临时录像已删除。";
        }
        private void AfterRecordingDecision()
        {
            recordingPanel.Hide();
            if (leavingBattle) SceneManager.LoadScene(MainMenuController.MenuScene);
        }
        private void LeaveBattle()
        {
            if (leavingBattle) return;
            leavingBattle = true;
            StartCoroutine(LeaveAfterNotification());
        }
        private System.Collections.IEnumerator LeaveAfterNotification()
        {
            if (network != null && network.ClientManager.Started)
            {
                network.ClientManager.Broadcast(new NetworkRoomLeave());
                yield return new WaitForSecondsRealtime(.15f);
            }
            FinishRecording("离开对局，录制结束"); StopSession();
            if (recording == null || !recording.HasFrames) SceneManager.LoadScene(MainMenuController.MenuScene);
        }
        private void OnApplicationQuit() { quitting = true; DiscardRecording(); }

    }
}
