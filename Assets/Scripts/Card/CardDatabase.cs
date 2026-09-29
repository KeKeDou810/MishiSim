using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class CardDatabase
{
    private Dictionary<string, CardDefinition> cards =
        new Dictionary<string, CardDefinition>(StringComparer.Ordinal);

    public int Count => cards.Count;

    public IEnumerable<CardDefinition> All => cards.Values;

    public void LoadDirectory(string directory)
    {
        // 全部加载成功后再替换，失败时保留原数据库。
        var loaded = new Dictionary<string, CardDefinition>(
            StringComparer.Ordinal);

        string[] paths = Directory.GetFiles(
            directory,
            "*.lua",
            SearchOption.AllDirectories);

        Array.Sort(paths, StringComparer.Ordinal);

        foreach (string path in paths)
        {
            try
            {
                string source = File.ReadAllText(path, Encoding.UTF8);
                CardDefinition card = LuaCardLoader.Load(source);

                if (loaded.ContainsKey(card.Id))
                {
                    throw new InvalidDataException(
                        $"重复卡片 ID：{card.Id}");
                }

                loaded.Add(card.Id, card);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"加载卡片失败：{path}\n{exception.Message}",
                    exception);
            }
        }

        var groups = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        foreach (CardDefinition card in loaded.Values)
        {
            // Any member can validate the shared rules; no printing is a required base version.
            if (!groups.TryGetValue(card.RulesId, out var peer))
                groups.Add(card.RulesId, peer = card);
            if (card.Clock != peer.Clock || card.Name != peer.Name || card.Type != peer.Type || card.Faction != peer.Faction ||
                card.Level != peer.Level || card.Power != peer.Power ||
                card.Sign != peer.Sign || card.Race != peer.Race || card.EffectText != peer.EffectText ||
                card.PlayerCardId1 != peer.PlayerCardId1 || card.PlayerCardId2 != peer.PlayerCardId2)
                throw new InvalidDataException($"同卡组 {card.RulesId} 的版本 {card.Id} 与 {peer.Id} 规则字段不一致。");
            if (!card.IsContract) continue;
            foreach (string playerId in new[] { card.PlayerCardId1, card.PlayerCardId2 })
            {
                if (playerId == null) continue; // Older contracts may still have pending slots.
                if (!loaded.TryGetValue(playerId, out CardDefinition player) || !player.IsPlayer)
                    throw new InvalidDataException($"契约 {card.Id} 引用的 {playerId} 不存在或不是玩家卡。");
            }
        }
        cards = loaded;
    }

    public CardDefinition Get(string id)
    {
        if (!cards.TryGetValue(id, out CardDefinition card))
        {
            throw new KeyNotFoundException(
                $"卡片数据库中不存在 ID：{id}");
        }

        return card;
    }
}
