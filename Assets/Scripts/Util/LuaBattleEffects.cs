using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Effects;
using MoonSharp.Interpreter;

// MoonSharp lives outside Mishi.Battle; the kernel depends only on ICardEffectProvider.
public sealed partial class LuaBattleEffects : ICardEffectProvider, IScryEffectProvider, ICardNameProvider, IContinuousEffectProvider, IAutomaticEffectProvider, Mishi.Battle.Events.IEventEffectProvider, IForesightEffectProvider
{
    public IReadOnlyList<string> CardNames { get; }
    private sealed class Entry { public Script Script; public Table Table; public CardEffectRules Rules; }
    private readonly KernelEffectRegistry registry = KernelEffectRegistry.CreateDefault();
    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    public LuaBattleEffects(IEnumerable<CardDefinition> definitions)
    {
        foreach (var card in definitions)
        {
            var script = new Script(CoreModules.Preset_HardSandbox);
            var value = Run(script, script.LoadString(card.ScriptSource), null);
            Table table = value.Table.Get("effects").Type == DataType.Table ? value.Table.Get("effects").Table : new Table(script);
            var rules = new CardEffectRules { Name = card.Name, Faction = card.Faction, Race = card.Race, Type = card.Type, Sign = card.Sign,
                ForesightCount = ForesightRules.Count(card.Sign, card.IsContract), ForesightMark = ForesightRules.Mark(card.Sign),
                Power = card.Power, Time = card.Level, IsToken = card.IsToken, OwnTurnOnly = ReadPlayTurn(table),
                HasPlay = table.Get("onPlay").Type == DataType.Function, HasActivate = table.Get("onActivate").Type == DataType.Function,
                ActivateCost = Number(table, "activateCost", 0, 0, 100), OncePerTurn = Boolean(table, "oncePerTurn"), OncePerNamePerTurn = Boolean(table, "oncePerNamePerTurn"),
                ActivateZone = Parse<TestCardZone>(String(table, "activateZone", "Board")),
                ActivationCosts = ReadActivationCosts(table),
                PlayCosts = ReadActivationCosts(table, "playCosts"),
                AttackTimeLimit = Number(table, "attackTimeLimit", int.MaxValue, 0, int.MaxValue) };
            if (table.Get("aura").Type == DataType.Table)
            {
                var aura = table.Get("aura").Table;
                rules.AuraRace = String(aura, "race", ""); rules.AuraPower = Number(aura, "power", 0, -100000, 100000);
                rules.AuraZone = Parse<TestCardZone>(String(aura, "zone", "OffField")); rules.AuraOwnTurn = Boolean(aura, "ownTurn", true);
            }
            ValidateExtensions(table);
            entries.Add(card.Id, new Entry { Script = script, Table = table, Rules = rules });
            IndexSubscriptions(card.Id, table);
        }
        CardNames = Array.AsReadOnly(entries.Values.Select(e => e.Rules.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }
    private static bool ReadPlayTurn(Table table)
    {
        var current = table.Get("playTurn");
        var legacy = table.Get("playTiming");
        if (current.Type != DataType.Nil && legacy.Type != DataType.Nil)
            throw new FormatException("Use playTurn only; do not combine it with legacy playTiming.");
        if (current.Type != DataType.Nil)
        {
            if (current.Type != DataType.String || (current.String != "own" && current.String != "either"))
                throw new FormatException("playTurn must be own or either.");
            return current.String == "own";
        }
        if (legacy.Type == DataType.Nil) return true;
        if (legacy.Type != DataType.String || (legacy.String != "ownTurn" && legacy.String != "response"))
            throw new FormatException("Legacy playTiming must be ownTurn or response; prefer playTurn.");
        return legacy.String == "ownTurn";
    }
    public CardEffectRules Rules(string id) => entries[id].Rules;
    public bool CanPlay(EffectContext context) => Condition(context, "canPlay");
    public bool CanActivate(EffectContext context) => Condition(context, "canActivate");
    private bool Condition(EffectContext context, string name)
    {
        var entry = entries[context.DefinitionId]; var function = entry.Table.Get(name);
        if (function.Type == DataType.Nil) return true;
        var result = Run(entry.Script, function, ToLua(entry.Script, context));
        if (result.Type != DataType.Boolean) throw new FormatException(name + " must return boolean.");
        return result.Boolean;
    }
    public IReadOnlyList<EffectInstruction> Build(EffectContext context)
    {
        if (context.Event == EffectEvent.Triggered) return Array.Empty<EffectInstruction>();
        var entry = entries[context.DefinitionId];
        string name = context.Event == EffectEvent.Played ? "onPlay" : context.Event == EffectEvent.Summoned ? "onSummon" : context.Event == EffectEvent.Destroyed ? "onDestroyed" : "onActivate";
        var function = entry.Table.Get(name);
        if (function.Type == DataType.Nil) return Array.Empty<EffectInstruction>();
        var result = Run(entry.Script, function, ToLua(entry.Script, context));
        return ParsePlan(entry, result);
    }
    public IReadOnlyList<EffectInstruction> BuildScry(EffectContext context, string callback)
    {
        var entry = entries[context.DefinitionId];
        return ParsePlan(entry, Run(entry.Script, entry.Table.Get(callback), ToLua(entry.Script, context)));
    }
    public IReadOnlyList<EffectInstruction> BuildForesight(EffectContext context)
    {
        var entry = entries[context.DefinitionId]; var function = entry.Table.Get("onForesight");
        return function.Type == DataType.Nil ? null : ParsePlan(entry, Run(entry.Script, function, ToLua(entry.Script, context)));
    }
    private IReadOnlyList<EffectInstruction> ParsePlan(Entry entry, DynValue result, int depth = 0)
    {
        if (depth > 3) throw new FormatException("Effect continuation nesting exceeds 3 levels.");
        if (result.Type != DataType.Table || result.Table.Length > 32) throw new FormatException("Effect must return an array of at most 32 instructions.");
        var instructions = new List<EffectInstruction>();
        for (int i = 1; i <= result.Table.Length; i++)
        {
            var value = result.Table.Get(i);
            if (value.Type != DataType.Table) throw new FormatException("Instruction must be table.");
            var t = value.Table;
            var operationId = registry.Get(String(t, "op", "")).Id;
            Enum.TryParse<EffectOp>(operationId, true, out var legacyOp);
            var step = new EffectInstruction { Op = legacyOp, EffectId = operationId, Target = String(t, "target", "self"), Side = String(t, "side", "own"),
                Key = String(t, "key", ""), Scope = Parse<VariableScope>(String(t, "scope", "effect")), Visibility = String(t, "visibility", "private"),
                Value = t.Get("value").Type == DataType.Table || t.Get("value").Type == DataType.Nil ? null : ReadTypedValue(t.Get("value"), String(t, "valueType", "")),
                ValueReference = t.Get("value").Type == DataType.Table ? ReadVariableReference(t.Get("value")) : null,
                StoreAs = String(t, "storeAs", ""), FromSet = String(t, "set", ""), Except = String(t, "except", ""),
                Callback = String(t, "callback", ""), Placement = String(t, "placement", "any"), PlacementNode = Number(t, "node", -1, -1, 100000), NameMatch = String(t, "nameMatch", "exact"), Types = ReadCardTypes(t.Get("types")),
                MinimumCount = Number(t, "minCount", 1, 0, 32), MaximumCount = Number(t, "maxCount", 1, 0, 32),
                OncePerTurn = t.Get("oncePerTurn").Type == DataType.Nil ? (bool?)null : Boolean(t, "oncePerTurn"),
                OncePerNamePerTurn = t.Get("oncePerNamePerTurn").Type == DataType.Nil ? (bool?)null : Boolean(t, "oncePerNamePerTurn"),
                Type = String(t, "type", ""), Name = String(t, "name", ""), Race = String(t, "race", ""), DefinitionId = String(t, "definitionId", ""),
                NotRebuiltThisTurn = Boolean(t, "notRebuiltThisTurn"),
                EnteredThisTurn = Boolean(t, "enteredThisTurn"),
                Timing = String(t, "timing", "turnEnd"), Costs = ReadActivationCosts(t, "costs"),
                Amount = t.Get("amount").Type == DataType.Table ? 0 : Number(t, "amount", 0, -100000, 100000),
                AmountReference = t.Get("amount").Type == DataType.Table ? ReadVariableReference(t.Get("amount")) : null, Minimum = Number(t, "minTime", 0, 0, 100000), Maximum = Number(t, "maxTime", int.MaxValue, 0, int.MaxValue),
                Zone = Parse<TestCardZone>(String(t, "zone", "Board")), Stat = String(t, "stat", "power"), Duration = String(t, "duration", "turn"),
                Mode = String(t, "mode", "add"), MaximumCost = Number(t, "maxCost", 11, 0, 11), MinimumDamage = Number(t, "minDamage", 1, 0, 11), MoveCost = Boolean(t, "moveCost", false),
                Prompt = String(t, "prompt", operationId == "Scry" ? "查看牌库" : "请选择效果目标"), Optional = Boolean(t, "optional"), Append = Boolean(t, "append"),
                From = String(t, "from", "top"), Position = String(t, "position", "top"), Reveal = Boolean(t, "reveal"), ScryIndex = Number(t, "index", 0, 0, 32) };
            if (t.Get("destination").Type != DataType.Nil || operationId == "Scry" && t.Get("position").Type != DataType.Nil)
                throw new FormatException("Use a ReturnToDeck instruction with position in Scry.after to place cards on top/bottom.");
            var after = t.Get("after");
            if (after.Type == DataType.Table) step.After = ParsePlan(entry, after, depth + 1);
            else if (after.Type == DataType.String)
            {
                if (entry.Table.Get(after.String).Type != DataType.Function) throw new FormatException("Missing Scry callback: " + after.String);
                step.AfterCallback = after.String;
            }
            else if (after.Type != DataType.Nil) throw new FormatException("after must be an instruction array or callback name.");
            if (operationId != "Scry" && operationId != "Schedule" && operationId != "Pay" && after.Type != DataType.Nil) throw new FormatException("after is only supported by Scry, Schedule and Pay.");
            registry.Validate(step, this);
            instructions.Add(step);
        }
        return instructions;
    }
    private static DynValue Run(Script script, DynValue function, DynValue argument)
    {
        if (function.Type != DataType.Function) throw new FormatException("Effect callback must be a Lua function.");
        var coroutine = script.CreateCoroutine(function).Coroutine;
        coroutine.AutoYieldCounter = 20000;
        var value = argument == null ? coroutine.Resume() : coroutine.Resume(argument);
        if (coroutine.State != CoroutineState.Dead) throw new InvalidOperationException("Lua instruction budget exceeded (20000); yielding is not allowed.");
        return value;
    }
    private static DynValue ToLua(Script script, EffectContext c)
    {
        var t = new Table(script);
        t.Set("node", DynValue.NewNumber(c.Node)); t.Set("instanceId", DynValue.NewString(c.InstanceId ?? "")); t.Set("time", DynValue.NewNumber(c.Time)); t.Set("power", DynValue.NewNumber(c.Power));
        t.Set("owner", DynValue.NewNumber(c.Owner)); t.Set("activePlayer", DynValue.NewNumber(c.ActivePlayer));
        t.Set("turn", DynValue.NewNumber(c.Turn)); t.Set("cost", DynValue.NewNumber(c.Cost)); t.Set("damage", DynValue.NewNumber(c.Damage));
        t.Set("handCount", DynValue.NewNumber(c.HandCount)); t.Set("clock", DynValue.NewString(c.Clock == ClockKind.White ? "white" : "black"));
        t.Set("emptyBoardCount", DynValue.NewNumber(c.EmptyBoardCount)); t.Set("contractZoneCount", DynValue.NewNumber(c.ContractZoneCount));
        t.Set("opponentClock", DynValue.NewString(c.OpponentClock == ClockKind.White ? "white" : "black"));
        t.Set("opponentCost", DynValue.NewNumber(c.OpponentCost)); t.Set("opponentDamage", DynValue.NewNumber(c.OpponentDamage));
        t.Set("opponentHandCount", DynValue.NewNumber(c.OpponentHandCount)); t.Set("publicCards", DynValue.NewTable(CardsToLua(script, c.PublicCards)));
        t.Set("contractName", DynValue.NewString(c.ContractName ?? ""));
        t.Set("zone", DynValue.NewString(c.SourceZone.ToString()));
        t.Set("event", EventToLua(script, c.TriggerEvent, c.Owner));
        var counts = new Table(script);
        if (c.OwnFieldNameCounts != null) foreach (var pair in c.OwnFieldNameCounts) counts.Set(pair.Key, DynValue.NewNumber(pair.Value));
        t.Set("counts", DynValue.NewTable(counts));
        t.Set("scry", DynValue.NewTable(CardsToLua(script, c.ScryCards)));
        var results = new Table(script);
        foreach (var pair in c.Results) results.Set(pair.Key, DynValue.NewString(pair.Value));
        t.Set("results", DynValue.NewTable(results));
        var sets = new Table(script);
        foreach (var pair in c.Sets) sets.Set(pair.Key, DynValue.NewTable(CardsToLua(script, pair.Value)));
        t.Set("sets", DynValue.NewTable(sets));
        var scopes = new Table(script);
        foreach (var scope in c.Variables)
        {
            var values = new Table(script);
            foreach (var pair in scope.Value) values.Set(pair.Key, ValueToLua(pair.Value));
            scopes.Set(scope.Key.ToString().ToLowerInvariant(), DynValue.NewTable(values));
        }
        t.Set("vars", DynValue.NewTable(scopes));
        t.Set("reason", DynValue.NewString(c.Reason ?? "")); return DynValue.NewTable(t);
    }
    private static Table CardsToLua(Script script, ScryCardInfo[] cards)
    {
        var viewed = new Table(script);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = cards[i]; var item = new Table(script);
            item.Set("instanceId", DynValue.NewString(card.InstanceId ?? "")); item.Set("zone", DynValue.NewString(card.Zone.ToString())); item.Set("node", DynValue.NewNumber(card.Node));
            item.Set("lastRebuiltTurn", DynValue.NewNumber(card.LastRebuiltTurn));
            item.Set("lastEffectRebuiltTurn", DynValue.NewNumber(card.LastEffectRebuiltTurn));
            item.Set("zoneEnteredTurn", DynValue.NewNumber(card.ZoneEnteredTurn));
            item.Set("id", DynValue.NewString(card.DefinitionId)); item.Set("name", DynValue.NewString(card.Name ?? ""));
            item.Set("type", DynValue.NewString(card.Type ?? "")); item.Set("race", DynValue.NewString(card.Race ?? ""));
            item.Set("sign", DynValue.NewString(card.Sign ?? "")); item.Set("owner", DynValue.NewNumber(card.Owner));
            item.Set("power", DynValue.NewNumber(card.Power)); item.Set("time", DynValue.NewNumber(card.Time));
            viewed.Set(i + 1, DynValue.NewTable(item));
        }
        return viewed;
    }
    private static DynValue ValueToLua(EffectValue value) => value.Type == EffectValueType.String ? DynValue.NewString(value.Text) :
        value.Type == EffectValueType.Boolean ? DynValue.NewBoolean(value.Boolean) : DynValue.NewNumber(value.Number);
    private static VariableReference ReadVariableReference(DynValue value)
    {
        var reference = new VariableReference { Key = String(value.Table, "var", ""), Scope = Parse<VariableScope>(String(value.Table, "scope", "effect")),
            Side = String(value.Table, "side", "own"), Target = String(value.Table, "target", "self") };
        reference.Validate(); return reference;
    }
    private static EffectValue ReadTypedValue(DynValue value, string declared)
    {
        EffectValue result;
        if (value.Type == DataType.String) result = EffectValue.String(value.String);
        else if (value.Type == DataType.Boolean) result = EffectValue.Bool(value.Boolean);
        else if (value.Type == DataType.Number)
        {
            double n = value.Number;
            if (double.IsNaN(n) || double.IsInfinity(n) || Math.Abs(n) > 1000000000) throw new FormatException("Variable number out of bounds.");
            result = declared.Equals("number", StringComparison.OrdinalIgnoreCase) || n != Math.Truncate(n) ? EffectValue.Decimal(n) : EffectValue.Integer((int)n);
        }
        else throw new FormatException("Variable value must be an integer, number, boolean or string.");
        if (!string.IsNullOrEmpty(declared) && Parse<EffectValueType>(declared) != result.Type) throw new FormatException("Variable valueType does not match its value.");
        return result;
    }
    private static T Parse<T>(string s) where T : struct => Enum.TryParse<T>(s, true, out var value) && Enum.IsDefined(typeof(T), value) ? value : throw new FormatException("Invalid " + typeof(T).Name + ": " + s);
    private static string String(Table t, string key, string fallback)
    { var v = t.Get(key); if (v.Type == DataType.Nil) return fallback; if (v.Type != DataType.String) throw new FormatException(key + " must be string."); return v.String; }
    private static bool Boolean(Table t, string key, bool fallback = false)
    { var v = t.Get(key); if (v.Type == DataType.Nil) return fallback; if (v.Type != DataType.Boolean) throw new FormatException(key + " must be boolean."); return v.Boolean; }
    private static int Number(Table t, string key, int fallback, int min, int max)
    { var v = t.Get(key); if (v.Type == DataType.Nil) return fallback; if (v.Type != DataType.Number || double.IsNaN(v.Number) || double.IsInfinity(v.Number) || v.Number < min || v.Number > max || v.Number != Math.Truncate(v.Number)) throw new FormatException(key + " invalid integer."); return (int)v.Number; }
}
