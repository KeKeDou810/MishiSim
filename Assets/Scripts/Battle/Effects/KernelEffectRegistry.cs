using System;
using System.Collections.Generic;
using System.Linq;
namespace Mishi.Battle.Effects
{
    public sealed class KernelEffectRegistry
    {
        private readonly Dictionary<string, KernelEffect> handlers = new Dictionary<string, KernelEffect>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<KernelEffect> All => Array.AsReadOnly(handlers.Values.OrderBy(effect => effect.Id, StringComparer.Ordinal).ToArray());
        public KernelEffectRegistry(IEnumerable<KernelEffect> effects)
        {
            foreach (var effect in effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Id)) throw new ArgumentException("Effect ID is required.");
                if (handlers.ContainsKey(effect.Id)) throw new ArgumentException("Duplicate effect ID: " + effect.Id);
                handlers.Add(effect.Id, effect);
            }
        }
        public static KernelEffectRegistry CreateDefault() => new KernelEffectRegistry(GeneratedKernelEffectCatalog.Create());
        public KernelEffect Get(string id) => id != null && handlers.TryGetValue(id, out var effect)
            ? effect : throw new FormatException("Unknown kernel effect: " + id);
        public void Validate(EffectInstruction instruction, ICardEffectProvider definitions)
        {
            EffectInstructionValidation.Common(instruction);
            // Deferred integer operands are checked again after resolving against execution state.
            var structural = instruction;
            if (instruction.AmountReference != null) { structural = instruction.Copy(); structural.Amount = 1; }
            Get(instruction.OperationId).Validate(structural, definitions);
        }
        public void Execute(EffectExecutionContext context, EffectInstruction instruction, ICardEffectProvider definitions)
        {
            if (instruction.AmountReference != null || instruction.ValueReference != null) throw new InvalidOperationException("Variable references must be resolved before execution.");
            Validate(instruction, definitions);
            Get(instruction.OperationId).Execute(context, instruction);
        }
    }
}
