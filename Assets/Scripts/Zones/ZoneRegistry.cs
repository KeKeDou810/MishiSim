using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mishi.Battle;

// Scoped by scene so additive scenes cannot overwrite one another's zone IDs.
public static class ZoneRegistry
{
    private static readonly Dictionary<(Scene, string), CardPlacementZoneView> zones = new Dictionary<(Scene, string), CardPlacementZoneView>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { zones.Clear(); }
    public static void Register(CardPlacementZoneView zone)
    {
        var key = (zone.gameObject.scene, zone.ZoneId);
        if (zones.TryGetValue(key, out var existing) && existing != null && existing != zone)
            throw new InvalidOperationException($"重复 ZoneId：{zone.ZoneId}");
        zones[key] = zone;
    }
    public static void Unregister(CardPlacementZoneView zone)
    {
        if (string.IsNullOrWhiteSpace(zone.ZoneId)) return;
        var key = (zone.gameObject.scene, zone.ZoneId);
        if (zones.TryGetValue(key, out var existing) && existing == zone) zones.Remove(key);
    }
    public static CardPlacementZoneView Get(Scene scene, string zoneId) => zones[(scene, zoneId)];
    public static IReadOnlyList<CardPlacementZoneView> InScene(Scene scene) =>
        zones.Where(p => p.Key.Item1 == scene && p.Value != null).Select(p => p.Value).ToArray();
    public static BattleBoard BuildBattleBoard(Scene scene)
    {
        var board = new BattleBoard();
        var nodes = InScene(scene).OfType<BoardNodeView>().ToArray();
        var ids = new HashSet<int>();
        foreach (var node in nodes)
        {
            if (!ids.Add(node.NodeId)) throw new InvalidOperationException($"重复 NodeId：{node.NodeId}");
            if (node.NodeKind == BoardNodeKind.Player || node.NodeKind == BoardNodeKind.Defense)
            {
                if (node.OwnerId < 0) throw new InvalidOperationException("玩家/防御圆阵需要 OwnerId。");
                board.SetPlayerOrDefenseNode(node.NodeId, node.OwnerId);
                if (node.NodeKind == BoardNodeKind.Player) board.SetPlayerNode(node.NodeId, node.OwnerId);
            }
            foreach (var neighbour in node.Neighbours)
            {
                if (neighbour == null || !nodes.Contains(neighbour) || neighbour == node)
                    throw new InvalidOperationException($"{node.ZoneId} 邻接配置无效。");
                board.Connect(node.NodeId, neighbour.NodeId);
            }
        }
        return board;
    }
}
