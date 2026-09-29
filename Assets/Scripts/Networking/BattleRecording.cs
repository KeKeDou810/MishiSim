using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Mishi.Networking
{
    public sealed class RecordingHeader
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public string Format = "mishi-public-replay", Visibility = "public", MatchId, ContentHash, ModeId, CreatedUtc;
    }
    public sealed class RecordingEntry
    {
        public string Kind;
        public double Seconds;
        public NetworkPublicFrame Frame;
        public NetworkTurnTimer Timer;
        public string Reason;
    }
    // JSON lines: one header, then independently flushed snapshots/timers, then an end marker.
    // This class has no Unity lifecycle or networking dependency beyond the wire DTOs.
    public sealed class BattleRecording : IDisposable
    {
        public const string Extension = ".mishi-replay";
        private readonly string directory;
        private readonly string matchId;
        private StreamWriter writer;
        private FileStream stream;
        private double origin = -1, lastSeconds;
        private long lastFrame;
        private bool finished, resolved;
        public string TemporaryPath { get; }
        public bool HasFrames => lastFrame > 0;
        public bool Finished => finished;
        public static string DirectoryFor(string contentRoot) => Path.Combine(contentRoot, "Recordings");
        public static string[] SavedFiles(string contentRoot)
        {
            var folder = DirectoryFor(contentRoot);
            return Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + Extension).OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>();
        }
        public BattleRecording(string contentRoot, RecordingHeader header)
        {
            directory = Path.GetFullPath(DirectoryFor(contentRoot)); Directory.CreateDirectory(directory);
            matchId = header.MatchId;
            header.CreatedUtc = DateTime.UtcNow.ToString("O");
            TemporaryPath = Path.Combine(directory, ".pending-" + Guid.NewGuid().ToString("N") + ".tmp");
            stream = new FileStream(TemporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            writer = new StreamWriter(stream, new UTF8Encoding(false));
            try { Write(header); }
            catch
            {
                CloseWriter();
                if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
                throw;
            }
        }
        public bool Append(NetworkPublicFrame frame)
        {
            if (finished || frame.Sequence <= lastFrame || frame.State.MatchId != matchId) return false;
            ValidatePublic(frame.State);
            if (origin < 0) origin = frame.ElapsedSeconds;
            Write(new RecordingEntry { Kind = "frame", Seconds = Time(frame.ElapsedSeconds), Frame = frame });
            lastFrame = frame.Sequence; return true;
        }
        public void Append(NetworkTurnTimer timer)
        {
            if (finished || !HasFrames || timer.MatchId != matchId) return;
            Write(new RecordingEntry { Kind = "timer", Seconds = Time(timer.ElapsedSeconds), Timer = timer });
        }
        private double Time(double absolute)
        {
            if (double.IsNaN(absolute) || double.IsInfinity(absolute)) throw new InvalidDataException("无效录像时间。");
            return lastSeconds = Math.Max(lastSeconds, Math.Max(0, absolute - origin));
        }
        public void Finish(string reason)
        {
            if (finished) return;
            Write(new RecordingEntry { Kind = "end", Seconds = lastSeconds, Reason = reason });
            // Keep the file lease until the user decides; another running client must not
            // mistake this pending save dialog for an abandoned recording.
            finished = true;
        }
        public string Save(string name)
        {
            if (resolved) throw new InvalidOperationException("录像已处理。");
            if (!HasFrames) throw new InvalidOperationException("暂无可保存的对局内容。");
            var clean = ValidateName(name);
            Finish("停止录制");
            string path = Path.Combine(directory, clean + Extension);
            for (int number = 2; File.Exists(path); number++) path = Path.Combine(directory, clean + " (" + number + ")" + Extension);
            CloseWriter();
            try { File.Move(TemporaryPath, path); }
            catch
            {
                stream = new FileStream(TemporaryPath, FileMode.Open, FileAccess.Write, FileShare.Read);
                stream.Seek(0, SeekOrigin.End); writer = new StreamWriter(stream, new UTF8Encoding(false)); throw;
            }
            resolved = true; return path;
        }
        public static string ValidateName(string name)
        {
            var clean = (name ?? "").Trim();
            if (clean.Length == 0 || clean.Length > 80 || clean == "." || clean == ".." || clean.EndsWith(".") ||
                clean.IndexOfAny(Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*".ToCharArray()).ToArray()) >= 0 || clean.Any(char.IsControl))
                throw new ArgumentException("名称需为 1–80 个字符，不能含路径或文件名禁用字符。");
            string stem = clean.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
                throw new ArgumentException("此名称为系统保留名称。");
            return clean;
        }
        public static void ValidatePublic(NetworkSnapshot state)
        {
            if (!state.Spectator || state.OwnHand == null || state.OwnHand.Length != 0 ||
                (state.ChoiceCards?.Length ?? 0) != 0 || (state.ChoiceNames?.Length ?? 0) != 0 ||
                (state.ChoiceZones?.Length ?? 0) != 0 || (state.ChoiceTriggers?.Length ?? 0) != 0 ||
                (state.DeckPositions?.Length ?? 0) != 0 || (state.Variables?.Length ?? 0) != 0 ||
                !state.ChoicePublicView && (state.ViewedCards?.Length ?? 0) != 0)
                throw new InvalidDataException("录像只接受公开状态，不能写入玩家私密视图。");
            foreach (var card in (state.Board ?? Array.Empty<NetworkCardInfo>()).Concat(state.PublicPiles ?? Array.Empty<NetworkCardInfo>()).Concat(state.PlayerCards ?? Array.Empty<NetworkCardInfo>()))
                if (card.FaceDown && !string.IsNullOrEmpty(card.DefinitionId)) throw new InvalidDataException("公开状态含背面卡片身份。");
        }
        private void Write(object value)
        {
            writer.WriteLine(JsonConvert.SerializeObject(value, Formatting.None));
            writer.Flush(); stream.Flush(true);
        }
        private void CloseWriter() { writer?.Dispose(); writer = null; stream = null; }
        public void Dispose()
        {
            CloseWriter();
            if (!resolved && File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
            resolved = true;
        }
        // Only abandoned pending files can be deleted; other running clients hold their files open.
        public static void CleanAbandoned(string contentRoot)
        {
            string folder = DirectoryFor(contentRoot);
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, ".pending-*.tmp"))
                try
                {
                    using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    File.Delete(file);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
        }
    }
    public sealed class BattleReplay
    {
        public RecordingHeader Header { get; private set; }
        public List<RecordingEntry> Entries { get; } = new List<RecordingEntry>();
        public double Duration => Entries.Count == 0 ? 0 : Entries[Entries.Count - 1].Seconds;
        public static BattleReplay Load(string file)
        {
            var replay = new BattleReplay();
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 };
            using var reader = new StreamReader(file, Encoding.UTF8);
            replay.Header = JsonConvert.DeserializeObject<RecordingHeader>(reader.ReadLine() ?? "", settings);
            if (replay.Header == null || replay.Header.Format != "mishi-public-replay" || replay.Header.Version != RecordingHeader.CurrentVersion || replay.Header.Visibility != "public")
                throw new InvalidDataException("不支持的录像格式或版本。");
            double previous = 0; long sequence = 0; bool ended = false;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var entry = JsonConvert.DeserializeObject<RecordingEntry>(line, settings);
                if (entry == null || ended || double.IsNaN(entry.Seconds) || double.IsInfinity(entry.Seconds) || entry.Seconds < previous)
                    throw new InvalidDataException("录像时间线已损坏。");
                if (entry.Kind == "frame")
                {
                    BattleRecording.ValidatePublic(entry.Frame.State);
                    if (entry.Frame.State.MatchId != replay.Header.MatchId || entry.Frame.Sequence <= sequence) throw new InvalidDataException("录像场面序列已损坏。");
                    sequence = entry.Frame.Sequence;
                }
                else if (entry.Kind == "end") ended = true;
                else if (entry.Kind != "timer" || entry.Timer.MatchId != replay.Header.MatchId) throw new InvalidDataException("录像事件已损坏。");
                replay.Entries.Add(entry); previous = entry.Seconds;
            }
            if (sequence == 0 || !ended) throw new InvalidDataException("录像未完成，不能回放。");
            return replay;
        }
        public int FrameAt(double seconds)
        {
            int result = 0;
            for (int i = 0; i < Entries.Count && Entries[i].Seconds <= seconds; i++) if (Entries[i].Kind == "frame") result = i;
            return result;
        }
    }
}
