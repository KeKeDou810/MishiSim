using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MoreMountains.Tools;
using UnityEngine;

namespace Mishi.Networking
{
    // One local companion per game process/session. Only already-visible preview data crosses this boundary.
    public sealed class DesktopCardPreview : MMEventListener<CardPreviewRequestEvent>, IDisposable
    {
        [Serializable] private sealed class State
        {
            public int revision, focus;
            public string title = "Mishi · 卡片预览", artwork = "", details = "悬停游戏内卡片以查看。", log = "暂无操作记录。";
        }
        private readonly string executable, statePath;
        private readonly State state = new State();
        private Process process;
        private bool disposed;
        public string Error { get; private set; }
        public DesktopCardPreview(string contentRoot)
        {
            executable = Path.Combine(contentRoot, "Tools", "CardPreview", "Mishi.CardPreview.exe");
            string directory = Path.Combine(Application.temporaryCachePath, "CardPreview");
            Directory.CreateDirectory(directory);
            statePath = Path.Combine(directory, Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N") + ".json");
            MMEventManager.AddListener<CardPreviewRequestEvent>(this);
        }
        public bool Open()
        {
            if (disposed) return false;
            try
            {
                if (!File.Exists(executable)) throw new FileNotFoundException("请先运行 Tools/CardPreviewWindow/build.ps1 生成独立预览程序。", executable);
                state.focus++; Write();
                if (process == null || process.HasExited)
                {
                    process?.Dispose();
                    process = Process.Start(new ProcessStartInfo {
                        FileName = executable,
                        Arguments = "\"" + statePath + "\" " + Process.GetCurrentProcess().Id,
                        WorkingDirectory = Path.GetDirectoryName(executable),
                        UseShellExecute = false, CreateNoWindow = true
                    });
                }
                Error = null; return process != null;
            }
            catch (Exception e) { Error = "独立卡片预览启动失败：" + e.Message; return false; }
        }
        public void SetLog(string[] entries)
        {
            string log = entries == null || entries.Length == 0 ? "暂无操作记录。" : string.Join("\n\n", entries.Reverse());
            if (state.log == log) return;
            state.log = log; WriteSafely();
        }
        public void ClearCard()
        {
            state.title = "Mishi · 卡片预览"; state.artwork = ""; state.details = "悬停游戏内卡片以查看。"; WriteSafely();
        }
        public void OnMMEvent(CardPreviewRequestEvent e)
        {
            var card = e.Card;
            if (card == null || disposed) return;
            state.title = "Mishi · " + card.Name;
            state.artwork = Path.GetFullPath(Path.Combine(e.ContentRoot, card.ArtworkPath));
            state.details = card.Name + "\nID：" + card.Id + "\n国家：" + card.Faction + "\n" +
                (card.IsPlayer ? "契约附带玩家卡（不计入50张）\n" : "时间：" + (e.EffectiveTime ?? card.Level) + "\n" +
                    (card.IsDecision ? "" : "力量：" + (e.EffectivePower ?? card.Power) + "\n")) +
                (string.IsNullOrEmpty(card.Sign) ? "" : "标识：" + card.Sign + "\n") + "类型：" + card.Type + "\n" +
                (card.IsPlayer || card.IsDecision || string.IsNullOrEmpty(card.Race) ? "" : "种族：" + card.Race + "\n") +
                (string.IsNullOrEmpty(card.EffectText) ? "" : "\n效果：\n" + card.EffectText);
            state.details = Regex.Replace(state.details, @"</?(?:size|color|b|i|u|sprite)(?:\s[^>]*|=[^>]*)?>", "", RegexOptions.IgnoreCase);
            WriteSafely();
        }
        private void WriteSafely()
        {
            if (disposed) return;
            try { Write(); }
            catch (Exception e) { Error = "独立预览更新失败：" + e.Message; }
        }
        private void Write()
        {
            state.revision++;
            string temporary = statePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(state), new UTF8Encoding(false));
            if (File.Exists(statePath)) File.Replace(temporary, statePath, null);
            else File.Move(temporary, statePath);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; MMEventManager.RemoveListener<CardPreviewRequestEvent>(this);
            try
            {
                if (process != null && !process.HasExited)
                {
                    // Covers quitting Play Mode before the native window has created its HWND.
                    File.WriteAllText(statePath + ".stop", "");
                    process.CloseMainWindow();
                }
            }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            process?.Dispose(); process = null;
            try { File.Delete(statePath); File.Delete(statePath + ".tmp"); } catch (IOException) { }
        }
    }
}
