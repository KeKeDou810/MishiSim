using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using MoonSharp.Interpreter;
public sealed partial class LuaBattleEffects
{
    private static ActivationCost[] ReadActivationCosts(Table table, string field = "activationCosts")
    {
        var value = table.Get(field);
        if (value.Type == DataType.Nil) return Array.Empty<ActivationCost>();
        if (value.Type != DataType.Table || value.Table.Length > 8) throw new FormatException("activationCosts must contain at most 8 components.");
        var costs = new List<ActivationCost>(); var keys = new HashSet<string>();
        for (int i = 1; i <= value.Table.Length; i++)
        {
            var item = value.Table.Get(i); if (item.Type != DataType.Table) throw new FormatException("Cost must be a table.");
            var t = item.Table; int amount = Number(t, "amount", 0, 0, 32);
            var cost = new ActivationCost { Subject = String(t, "subject", "any"), Kind = Parse<ActivationCostKind>(String(t, "kind", "Time")), Minimum = Number(t, "min", amount, 0, 32),
                Zone = t.Get("zone").Type == DataType.Nil ? (TestCardZone?)null : Parse<TestCardZone>(String(t, "zone", "")),
                Zones = ReadCardTypes(t.Get("zones")).Select(Parse<TestCardZone>).ToArray(),
                Type = String(t, "type", ""), Race = String(t, "race", ""), Name = String(t, "name", ""), NameMatch = String(t, "nameMatch", "exact"),
                Position = String(t, "position", "bottom"), Destination = Parse<TestCardZone>(String(t, "destination", "Hand")), PaySelectedTime = Boolean(t, "paySelectedTime"), StoreTime = Boolean(t, "storeTime"),
                Maximum = Number(t, "max", amount, 0, 32), StoreAs = String(t, "storeAs", "") };
            cost.Validate();
            if (!string.IsNullOrEmpty(cost.StoreAs) && !keys.Add(cost.StoreAs)) throw new FormatException("Duplicate cost storeAs key.");
            costs.Add(cost);
        }
        return costs.ToArray();
    }
}
