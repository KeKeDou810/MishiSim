using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

public sealed class DeckModel
{
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
    private Dictionary<string, int> limits = new Dictionary<string, int>(StringComparer.Ordinal);
    public GameMode Mode { get; }
    public DeckModel(GameMode mode = null) { Mode = mode ?? new GameMode(); }
    public DeckModel EmptyCopy() => new DeckModel(Mode) { limits = new Dictionary<string, int>(limits, StringComparer.Ordinal) };
    public bool ValidateComplete(out string error)
    {
        error = Contract == null ? "请先选择契约。" : Count != Mode.DeckSize ? $"此模式需要 {Mode.DeckSize} 张卡（含契约，不含玩家卡），当前 {Count} 张。" : null;
        return error == null;
    }
    public IEnumerable<KeyValuePair<string, int>> Entries => counts;
    public CardDefinition Contract { get; private set; }
    public int Count => counts.Values.Sum();
    // Attached player slots are outside Entries and the 50-card count.
    public IReadOnlyList<string> PlayerCards => Contract == null
        ? Array.Empty<string>()
        : new[] { Contract.PlayerCardId1, Contract.PlayerCardId2 };

    public void LoadLimits(string path)
    {
        var loaded = File.Exists(path)
            ? JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(path))
            : new Dictionary<string, int>();
        if (loaded == null) throw new InvalidDataException("禁卡表必须是 JSON object。");
        foreach (var entry in loaded)
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0 || entry.Value > 200)
                throw new InvalidDataException($"无效禁卡表限制：{entry.Key}，数量必须为 0～200。");
        limits = new Dictionary<string, int>(loaded, StringComparer.Ordinal);
    }

    public bool TryAdd(CardDefinition card, out string error)
    {
        error = null;
        if (card == null) { error = "卡片不存在。"; return false; }
        if (card.IsPlayer) { error = "玩家卡由契约自动附带，不加入主卡组。"; return false; }
        if (string.IsNullOrWhiteSpace(card.Faction)) { error = $"{card.Id} 尚未配置国家。"; return false; }
        int limit = limits.TryGetValue(card.Id, out int configured) ? Math.Min(configured, Mode.MaxCopies) : Mode.MaxCopies;
        if (Mode.CardLimits.TryGetValue(card.Id, out int modeLimit)) limit = Math.Min(limit, modeLimit);
        counts.TryGetValue(card.Id, out int count);
        if (count >= limit)
        {
            error = limit == 0 ? $"{card.Id} 是禁卡。" : $"{card.Id} 最多可放 {limit} 张。";
            return false;
        }
        if (card.IsContract)
        {
            if (card.Level != 0) { error = "当前模式需要等级 0 的契约时魔。"; return false; }
            if (Contract != null) { error = "卡组只能有一张契约时魔。"; return false; }
        }
        else
        {
            if (Contract == null) { error = "请先加入契约时魔。"; return false; }
            if (Mode.RequireSameFaction && card.Faction != Mode.GenericFaction && !string.Equals(card.Faction, Contract.Faction, StringComparison.Ordinal))
            { error = $"只能加入国家为「{Contract.Faction}」或「{Mode.GenericFaction}」的卡片。"; return false; }
        }
        if (Count >= Mode.DeckSize) { error = $"卡组上限 {Mode.DeckSize} 张（包含契约，不包含玩家卡）。"; return false; }
        counts[card.Id] = count + 1;
        if (card.IsContract) Contract = card;
        return true;
    }

    public bool TryRemove(string id, out string error)
    {
        error = null;
        if (!counts.TryGetValue(id, out int count)) { error = "卡组中不存在该卡。"; return false; }
        if (Contract != null && Contract.Id == id)
        {
            if (Count > 1) { error = "请先移除其他卡片，再移除契约时魔。"; return false; }
            Contract = null;
        }
        if (count == 1) counts.Remove(id); else counts[id] = count - 1;
        return true;
    }
}

