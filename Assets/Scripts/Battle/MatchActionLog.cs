using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle.Events;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private readonly List<string>[] actionLogs = { new List<string>(), new List<string>() };
        private readonly List<string> publicActionLog = new List<string>();
        private long logSequence;
        public string[] ActionLogFor(int player) => (player < 0 ? publicActionLog : actionLogs[player]).ToArray();
        private void AddLog(int player, string text, int audience = -1)
        {
            string line = $"{++logSequence}. [回合 {Turn} · P{player + 1}] {text}";
            // -2 is public-only; private player logs are never used to reconstruct public logs.
            if (audience < 0)
            {
                publicActionLog.Add(line);
                if (publicActionLog.Count > 160) publicActionLog.RemoveAt(0);
            }
            for (int viewer = 0; viewer < 2; viewer++)
            {
                if (audience == -2) continue;
                if (audience >= 0 && viewer != audience) continue;
                actionLogs[viewer].Add(line);
                if (actionLogs[viewer].Count > 160) actionLogs[viewer].RemoveAt(0);
            }
        }
        private string CardLabel(Card card) => effects?.Rules(card.DefinitionId).Name ?? card.DefinitionId;
        private static string ZoneLabel(TestCardZone zone)
        {
            switch (zone)
            {
                case TestCardZone.Board: return "场上";
                case TestCardZone.Hand: return "手牌";
                case TestCardZone.Deck: return "牌库";
                case TestCardZone.Discard: return "弃牌区";
                case TestCardZone.Contract: return "契约区";
                case TestCardZone.Exile: return "除外区";
                case TestCardZone.OffField: return "场外区";
                case TestCardZone.Player: return "玩家区";
                default: return "离场区域";
            }
        }
        private void LogEvent(BattleEvent data)
        {
            if (data == null || data is VariableChangedEvent || data.Id == "LeftField" || data.Id == "Discarded" || data.Id == "Drawn" || data.Id == "PhaseEnded") return;
            for (int viewer = -1; viewer < 2; viewer++)
            {
                if (viewer < 0 ? !data.VisibleTo(0) || !data.VisibleTo(1) : !data.VisibleTo(viewer)) continue;
                string text;
                if (data is CardBattleEvent cardEvent)
                {
                    bool visible = viewer < 0 ? cardEvent.Card.VisibleTo(0) && cardEvent.Card.VisibleTo(1) : cardEvent.Card.VisibleTo(viewer);
                    string name = visible ? cardEvent.Card.Name : "一张卡";
                    string label = BattleEventRegistry.Definitions.TryGetValue(data.Id, out var value) ? value : data.Id;
                    text = data.Id == "CardMoved" ? $"{name}：{ZoneLabel(cardEvent.From)} → {ZoneLabel(cardEvent.To)}" : $"{name}：{label}";
                    if (data is AttackDeclaredEvent attack) text += " → " + (attack.Target == null ? $"P{attack.TargetPlayer + 1} 玩家" :
                        (viewer < 0 ? attack.Target.VisibleTo(0) && attack.Target.VisibleTo(1) : attack.Target.VisibleTo(viewer)) ? attack.Target.Name : "一张卡");
                }
                else if (data is PlayerBattleEvent change)
                    text = $"{(BattleEventRegistry.Definitions.TryGetValue(data.Id, out var label) ? label : data.Id)}：{change.Before} → {change.After}";
                else if (data is PhaseBattleEvent phase)
                    text = "进入阶段：" + new[] { "回合开始", "抽卡", "重构", "时间重构", "主要", "战斗", "结束" }[(int)phase.Phase];
                else continue;
                AddLog(data.Player, text, viewer < 0 ? -2 : viewer);
            }
        }
        private readonly Dictionary<Guid, string> loggedStats = new Dictionary<Guid, string>();
        private void LogStatChanges()
        {
            foreach (var card in cards.Where(c => c.Zone == TestCardZone.Board && !c.Covered))
            {
                string state = $"{card.Power}/{card.Time}/{card.AttackRange}";
                if (loggedStats.TryGetValue(card.Id, out var previous) && previous != state)
                {
                    var sources = card.ContinuousContributions.Select(m => cards.FirstOrDefault(c => c.Id == m.Source)).Where(c => c != null && PublicZone(c.Zone) && !c.HiddenAttachment).Select(CardLabel)
                        .Concat(card.Modifiers.Where(ModifierActive).Select(m => cards.FirstOrDefault(c => c.Id == m.Source)).Where(c => c != null && PublicZone(c.Zone) && !c.HiddenAttachment).Select(CardLabel)).Distinct();
                    AddLog(card.Owner, $"{CardLabel(card)} 力量/时间/距离：{previous} → {state}；当前修正来源：{string.Join("、", sources.DefaultIfEmpty("基础数值或修正到期"))}");
                }
                loggedStats[card.Id] = state;
            }
            foreach (var id in loggedStats.Keys.Where(id => !cards.Any(c => c.Id == id && c.Zone == TestCardZone.Board && !c.Covered)).ToArray()) loggedStats.Remove(id);
        }
    }
}
