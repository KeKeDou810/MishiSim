using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public static class DeckStorage
{
    public sealed class Document
    {
        public int Version = 1;
        public string ModeId;
        public Dictionary<string, int> Cards;
    }
    private static string FilePath(string directory, string name)
    {
        if (name == null || !Regex.IsMatch(name, @"\A[\p{L}\p{N} _-]{1,48}\z") || name != name.Trim())
            throw new InvalidDataException("卡组名称限 1～48 个中英文字、数字、空格、下划线或连字符，首尾不能有空格。");
        return Path.Combine(directory, "deck-" + name + ".json");
    }
    public static string[] List(string directory) => !Directory.Exists(directory) ? Array.Empty<string>() :
        Directory.GetFiles(directory, "deck-*.json").Select(Path.GetFileNameWithoutExtension).Select(n => n.Substring(5)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    public static void Save(string directory, string name, DeckModel deck)
    {
        string path = FilePath(directory, name);
        if (deck == null || string.IsNullOrWhiteSpace(deck.Mode.Id)) throw new InvalidDataException("请先加载有效的游戏模式。");
        var data = new Document { ModeId = deck.Mode.Id, Cards = deck.Entries.ToDictionary(e => e.Key, e => e.Value) };
        Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonConvert.SerializeObject(data, Formatting.Indented));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static Document Read(string directory, string name)
    {
        string path = FilePath(directory, name);
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("卡组存档过大。");
        var data = JsonConvert.DeserializeObject<Document>(File.ReadAllText(path), new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (data == null || data.Version != 1 || string.IsNullOrWhiteSpace(data.ModeId) || data.Cards == null ||
            data.Cards.Any(e => string.IsNullOrWhiteSpace(e.Key) || e.Value <= 0 || e.Value > 200) || data.Cards.Count > 200)
            throw new InvalidDataException("卡组存档格式或数量无效。");
        return data;
    }
    public static DeckModel Validate(Document data, DeckModel emptyDeck, Func<string, CardDefinition> getCard)
    {
        if (data.ModeId != emptyDeck.Mode.Id) throw new InvalidDataException("卡组模式不匹配。");
        if (data.Cards.Values.Sum(n => (long)n) > emptyDeck.Mode.DeckSize) throw new InvalidDataException("卡组超过模式张数上限。");
        var next = emptyDeck.EmptyCopy();
        foreach (var entry in data.Cards.Select(e => new { Card = getCard(e.Key), Count = e.Value }).OrderBy(e => e.Card.IsContract ? 0 : 1))
            for (int i = 0; i < entry.Count; i++)
                if (!next.TryAdd(entry.Card, out string error)) throw new InvalidDataException(error);
        return next;
    }
}
