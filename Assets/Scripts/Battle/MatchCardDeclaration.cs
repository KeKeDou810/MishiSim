using System;
using System.Linq;

namespace Mishi.Battle
{
    public sealed partial class NetworkTestMatch
    {
        private void BeginDeclaration(EffectExecution execution, EffectInstruction instruction)
        {
            var names = ((ICardNameProvider)effects).CardNames.Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (names.Length == 0) throw new FormatException("No card names available for declaration.");
            execution.PendingChoiceStore = instruction.StoreAs;
            effectChoice = new EffectChoice {
                Player = execution.Source.Owner, NameOptions = names,
                Prompt = instruction.Prompt ?? "宣言一个卡名",
                Seconds = drawRules?.ResponseTimeSeconds > 0 ? drawRules.ResponseTimeSeconds : 20
            };
        }
        private void ResolveDeclaration(int index)
        {
            var execution = executions.Peek();
            string name = effectChoice.NameOptions[index];
            AddLog(execution.Source.Owner, "宣言卡名：" + name);
            try { execution.Variables.Set(execution.PendingChoiceStore, EffectValue.String(name), execution.Source.Owner, false); }
            catch (Exception e) { StopForScriptError(e); return; }
            execution.PendingChoiceStore = null;
            lastAction = $"P{execution.Source.Owner + 1} 宣言卡名：{name}";
            effectChoice = null;
            DrainEffects(); RefreshStats();
        }
    }
}
