using System;
using System.Collections.Generic;
using Mishi.Battle;
using MoonSharp.Interpreter;

public sealed partial class LuaBattleEffects
{
    private static void ValidateExtensions(Table table)
    {
        var continuous = table.Get("continuous");
        if (continuous.Type != DataType.Nil && continuous.Type != DataType.Function) throw new FormatException("continuous must be a function.");
        var triggers = table.Get("triggers");
        if (triggers.Type == DataType.Nil) return;
        if (triggers.Type != DataType.Table || triggers.Table.Length > 32) throw new FormatException("triggers must contain at most 32 entries.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 1; i <= triggers.Table.Length; i++)
        {
            var value = triggers.Table.Get(i);
            if (value.Type != DataType.Table) throw new FormatException("Trigger must be a table.");
            var t = value.Table;
            ReadActivationCosts(t, "costs");
            string id = String(t, "id", ""); EffectVariableStore.ValidateKey(id);
            if (!ids.Add(id)) throw new FormatException("Duplicate trigger id: " + id);
            Mishi.Battle.Events.BattleEventRegistry.Validate(String(t, "event", ""));
            if (!LegacyTrigger(t)) ReadSubscription(t).Validate();
            if (t.Get("onTrigger").Type != DataType.Function) throw new FormatException("Trigger requires onTrigger function.");
            var condition = t.Get("condition");
            if (condition.Type != DataType.Nil && condition.Type != DataType.Function) throw new FormatException("Trigger condition must be a function.");
            if (String(t, "label", id).Length > 80) throw new FormatException("Trigger label is too long.");
        }
    }
    public IReadOnlyList<AutomaticEffectPlan> BuildAutomatic(EffectContext context)
    {
        var entry = entries[context.DefinitionId];
        var plans = new List<AutomaticEffectPlan>();
        var legacy = Build(context);
        if (legacy.Count > 0) plans.Add(new AutomaticEffectPlan { Label = context.Event == EffectEvent.Summoned ? "登场效果" : "被破坏效果", Steps = legacy });
        var triggers = entry.Table.Get("triggers");
        if (triggers.Type == DataType.Nil) return plans;
        for (int i = 1; i <= triggers.Table.Length; i++)
        {
            var t = triggers.Table.Get(i).Table;
            if (!LegacyTrigger(t)) continue;
            if (Parse<EffectEvent>(String(t, "event", "")) != context.Event) continue;
            var condition = t.Get("condition");
            if (condition.Type != DataType.Nil)
            {
                var result = Run(entry.Script, condition, ToLua(entry.Script, context));
                if (result.Type != DataType.Boolean) throw new FormatException("Trigger condition must return boolean.");
                if (!result.Boolean) continue;
            }
            string id = String(t, "id", "");
            var plan = ParsePlan(entry, Run(entry.Script, t.Get("onTrigger"), ToLua(entry.Script, context)));
            if (plan.Count > 0) plans.Add(new AutomaticEffectPlan { Id = id, Label = String(t, "label", id), Steps = plan, Costs = ReadActivationCosts(t, "costs") });
        }
        if (plans.Count > 32) throw new FormatException("At most 32 automatic effects, including legacy callbacks.");
        return plans;
    }
}
