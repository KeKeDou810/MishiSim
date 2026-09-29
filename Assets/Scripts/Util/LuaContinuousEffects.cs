using System;
using System.Collections.Generic;
using Mishi.Battle;
using MoonSharp.Interpreter;

public sealed partial class LuaBattleEffects
{
    public IReadOnlyList<ContinuousModifier> Continuous(EffectContext context)
    {
        var entry = entries[context.DefinitionId];
        var function = entry.Table.Get("continuous");
        if (function.Type == DataType.Nil) return Array.Empty<ContinuousModifier>();
        var result = Run(entry.Script, function, ToLua(entry.Script, context));
        if (result.Type != DataType.Table || result.Table.Length > 32) throw new FormatException("continuous must return at most 32 modifiers.");
        var modifiers = new List<ContinuousModifier>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i <= result.Table.Length; i++)
        {
            var value = result.Table.Get(i);
            if (value.Type != DataType.Table) throw new FormatException("Continuous modifier must be a table.");
            var t = value.Table;
            var modifier = new ContinuousModifier { Id = String(t, "id", ""), Stat = String(t, "stat", "power"),
                Target = String(t, "target", "self"), Side = String(t, "side", "own"), Amount = Number(t, "amount", 0, -100000, 100000),
                SourceZone = Parse<TestCardZone>(String(t, "sourceZone", "Board")), TargetZone = Parse<TestCardZone>(String(t, "zone", "Board")),
                Race = String(t, "race", ""), Type = String(t, "type", ""), Name = String(t, "name", "") };
            modifier.Validate();
            if (!ids.Add(modifier.Id)) throw new FormatException("Duplicate continuous modifier id: " + modifier.Id);
            modifiers.Add(modifier);
        }
        return modifiers;
    }
}
