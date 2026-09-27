using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Mishi.Battle;

// External, versioned configuration. New mechanics still require an engine implementation.
public sealed class GameMode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int DeckSize { get; set; } = 50;
    public int MaxCopies { get; set; } = 4;
    public bool RequireSameFaction { get; set; } = true;
    public string GenericFaction { get; set; } = "不明";
    public int OpeningHand { get; set; } = 5;
    public int DrawPerTurn { get; set; } = 1;
    public string[] FirstTurnSkip { get; set; } = { "Rebuild", "TimeReset", "Combat" };
    public Dictionary<string, int> ExampleDeck { get; set; }
    public Dictionary<string, int> CardLimits { get; set; } = new Dictionary<string, int>();
    public MatchDrawRules BattleRules() => new MatchDrawRules(OpeningHand, DrawPerTurn,
        FirstTurnSkip.Select(p => (TestTurnPhase)Enum.Parse(typeof(TestTurnPhase), p)));

    public static GameMode[] Load(string path)
    {
        var file = JsonConvert.DeserializeObject<ModeFile>(File.ReadAllText(path),
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (file == null || file.Version != 1 || file.Modes == null || file.Modes.Length == 0)
            throw new InvalidDataException("game-modes.json 需要 version: 1 和非空 modes。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mode in file.Modes)
        {
            if (mode == null || string.IsNullOrWhiteSpace(mode.Id) || !ids.Add(mode.Id) || string.IsNullOrWhiteSpace(mode.Name))
                throw new InvalidDataException("模式必须有唯一 id 和 name。");
            if (mode.DeckSize < 2 || mode.DeckSize > 200 || mode.MaxCopies < 1 || mode.MaxCopies > mode.DeckSize ||
                mode.OpeningHand < 0 || mode.OpeningHand >= mode.DeckSize || mode.DrawPerTurn < 0 || mode.DrawPerTurn > 20 ||
                string.IsNullOrWhiteSpace(mode.GenericFaction) || mode.FirstTurnSkip == null ||
                mode.FirstTurnSkip.Any(p => p != "Rebuild" && p != "TimeReset" && p != "Combat"))
                throw new InvalidDataException($"模式 {mode.Id} 的数量或首回合跳过阶段配置无效。");
            if (mode.ExampleDeck != null && mode.ExampleDeck.Any(e => string.IsNullOrWhiteSpace(e.Key) || e.Value < 1 || e.Value > mode.DeckSize))
                throw new InvalidDataException($"模式 {mode.Id} 的 exampleDeck 无效。");
            if (mode.CardLimits == null || mode.CardLimits.Any(e => string.IsNullOrWhiteSpace(e.Key) || e.Value < 0 || e.Value > mode.MaxCopies))
                throw new InvalidDataException($"模式 {mode.Id} 的 cardLimits 必须在 0 到 maxCopies 之间。");
        }
        return file.Modes;
    }
    private sealed class ModeFile { public int Version { get; set; } public GameMode[] Modes { get; set; } }
}
