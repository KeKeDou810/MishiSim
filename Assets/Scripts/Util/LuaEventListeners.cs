using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Events;
using MoonSharp.Interpreter;

public sealed partial class LuaBattleEffects
{
    private readonly Dictionary<string, EventSubscription[]> subscriptions = new Dictionary<string, EventSubscription[]>();
    private static bool LegacyTrigger(Table t) => t.Get("listen").Type == DataType.Nil &&
        (String(t, "event", "") == "Summoned" || String(t, "event", "") == "Destroyed");
    private static EventSubscription ReadSubscription(Table t)
    {
        var listen = t.Get("listen");
        if (listen.Type != DataType.Nil && listen.Type != DataType.Table) throw new FormatException("listen must be a table.");
        var filter = listen.Type == DataType.Table ? listen.Table : t;
        string id = String(t, "id", "");
        return new EventSubscription { Id = id, Event = String(t, "event", ""), Label = String(t, "label", id), Costs = ReadActivationCosts(t, "costs"),
            OncePerTurn = t.Get("oncePerTurn").Type == DataType.Nil ? (bool?)null : Boolean(t, "oncePerTurn"),
            OncePerNamePerTurn = t.Get("oncePerNamePerTurn").Type == DataType.Nil ? (bool?)null : Boolean(t, "oncePerNamePerTurn"),
            ActiveZone = Parse<TestCardZone>(String(t, "activeZone", "Board")),
            Subject = String(filter, "subject", "any"), Side = String(filter, "side", "any"),
            Name = String(filter, "name", ""), Type = String(filter, "type", ""), Race = String(filter, "race", ""), Key = String(filter, "key", ""),
            NameMatch = String(filter, "nameMatch", "exact"), Types = ReadCardTypes(filter.Get("types")),
            From = filter.Get("from").Type == DataType.Nil ? (TestCardZone?)null : Parse<TestCardZone>(String(filter, "from", "")),
            To = filter.Get("to").Type == DataType.Nil ? (TestCardZone?)null : Parse<TestCardZone>(String(filter, "to", "")),
            Phase = filter.Get("phase").Type == DataType.Nil ? (TestTurnPhase?)null : Parse<TestTurnPhase>(String(filter, "phase", "")),
            Scope = filter.Get("scope").Type == DataType.Nil ? (VariableScope?)null : Parse<VariableScope>(String(filter, "scope", "")) };
    }
    private static string[] ReadCardTypes(DynValue value)
    {
        if (value.Type == DataType.Nil) return Array.Empty<string>();
        if (value.Type != DataType.Table || value.Table.Length > 16) throw new FormatException("types must be an array of at most 16 card types.");
        var result = new string[value.Table.Length];
        for (int i = 0; i < result.Length; i++)
        { var item = value.Table.Get(i + 1); if (item.Type != DataType.String) throw new FormatException("Card type must be string."); result[i] = item.String; }
        return result;
    }
    private void IndexSubscriptions(string id, Table table)
    {
        var list = new List<EventSubscription>(); var triggers = table.Get("triggers");
        if (triggers.Type == DataType.Table)
            for (int i = 1; i <= triggers.Table.Length; i++)
                if (!LegacyTrigger(triggers.Table.Get(i).Table)) list.Add(ReadSubscription(triggers.Table.Get(i).Table));
        subscriptions.Add(id, list.ToArray());
    }
    public IReadOnlyList<EventSubscription> Subscriptions(string definitionId, string eventId) => subscriptions[definitionId].Where(s => s.Event == eventId).ToArray();
    public IReadOnlyList<EffectInstruction> BuildTriggered(EffectContext context, string triggerId)
    {
        var entry = entries[context.DefinitionId]; var triggers = entry.Table.Get("triggers").Table;
        for (int i = 1; i <= triggers.Length; i++)
        {
            var t = triggers.Get(i).Table;
            if (String(t, "id", "") != triggerId) continue;
            var condition = t.Get("condition");
            if (condition.Type != DataType.Nil)
            {
                var result = Run(entry.Script, condition, ToLua(entry.Script, context));
                if (result.Type != DataType.Boolean) throw new FormatException("Trigger condition must return boolean.");
                if (!result.Boolean) return Array.Empty<EffectInstruction>();
            }
            return ParsePlan(entry, Run(entry.Script, t.Get("onTrigger"), ToLua(entry.Script, context)));
        }
        throw new FormatException("Unknown trigger: " + triggerId);
    }
    private static DynValue EventToLua(Script script, BattleEvent data, int viewer)
    {
        if (data == null || !data.VisibleTo(viewer)) return DynValue.Nil;
        var t = new Table(script);
        t.Set("id", DynValue.NewString(data.Id)); t.Set("player", DynValue.NewNumber(data.Player));
        t.Set("reason", DynValue.NewString(data.Reason)); t.Set("source", EventCardToLua(script, data.Cause, viewer));
        if (data is CardBattleEvent card)
        { t.Set("card", EventCardToLua(script, card.Card, viewer)); t.Set("from", DynValue.NewString(card.From.ToString())); t.Set("to", DynValue.NewString(card.To.ToString())); }
        if (data is PlayerBattleEvent player)
        { t.Set("amount", DynValue.NewNumber(player.Amount)); t.Set("before", DynValue.NewNumber(player.Before)); t.Set("after", DynValue.NewNumber(player.After)); }
        if (data is AttackDeclaredEvent attack)
        { t.Set("target", EventCardToLua(script, attack.Target, viewer)); t.Set("targetPlayer", DynValue.NewNumber(attack.TargetPlayer)); t.Set("targetNode", DynValue.NewNumber(attack.TargetNode)); }
        if (data is ForesightResolvedEvent resolved)
        { var targets = new Table(script); for (int i = 0; i < resolved.Targets.Length; i++) targets.Set(i + 1, EventCardToLua(script, resolved.Targets[i], viewer)); t.Set("targets", DynValue.NewTable(targets)); }
        if (data is CardMovedEvent moved) t.Set("position", DynValue.NewString(moved.Position ?? ""));
        if (data is DeckPositionedEvent positioned) t.Set("position", DynValue.NewString(positioned.Position));
        if (data is BattleEndedEvent ended)
        { t.Set("attacker", EventCardToLua(script, ended.Attacker, viewer)); t.Set("target", EventCardToLua(script, ended.Target, viewer)); t.Set("targetPlayer", DynValue.NewNumber(ended.TargetPlayer)); t.Set("cancelled", DynValue.NewBoolean(ended.Cancelled)); }
        if (data is ForesightRevealedEvent foresight) t.Set("revealed", EventCardToLua(script, foresight.Revealed, viewer));
        if (data is PhaseBattleEvent phase)
        { t.Set("turn", DynValue.NewNumber(phase.Turn)); t.Set("phase", DynValue.NewString(phase.Phase.ToString())); }
        if (data is VariableChangedEvent variable)
        {
            t.Set("scope", DynValue.NewString(variable.Scope.ToString().ToLowerInvariant())); t.Set("key", DynValue.NewString(variable.Key));
            t.Set("before", variable.Before == null ? DynValue.Nil : ValueToLua(variable.Before));
            t.Set("after", variable.After == null ? DynValue.Nil : ValueToLua(variable.After)); t.Set("card", EventCardToLua(script, variable.Card, viewer));
        }
        return DynValue.NewTable(t);
    }
    private static DynValue EventCardToLua(Script script, EventCard card, int viewer)
    {
        if (card == null || !card.VisibleTo(viewer)) return DynValue.Nil;
        var t = new Table(script);
        t.Set("instanceId", DynValue.NewString(card.InstanceId.ToString("N"))); t.Set("id", DynValue.NewString(card.DefinitionId));
        t.Set("name", DynValue.NewString(card.Name ?? "")); t.Set("type", DynValue.NewString(card.Type ?? "")); t.Set("race", DynValue.NewString(card.Race ?? ""));
        t.Set("faction", DynValue.NewString(card.Faction ?? ""));
        t.Set("sign", DynValue.NewString(card.Sign ?? "")); t.Set("zone", DynValue.NewString(card.Zone.ToString())); t.Set("generation", DynValue.NewNumber(card.Generation));
        t.Set("owner", DynValue.NewNumber(card.Owner)); t.Set("power", DynValue.NewNumber(card.Power)); t.Set("time", DynValue.NewNumber(card.Time));
        t.Set("node", DynValue.NewNumber(card.Node)); return DynValue.NewTable(t);
    }
}
