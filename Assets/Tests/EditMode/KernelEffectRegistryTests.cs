using System;
using System.Collections.Generic;
using System.Linq;
using Mishi.Battle;
using Mishi.Battle.Effects;
using NUnit.Framework;

public sealed class KernelEffectRegistryTests
{
    private sealed class ProbeEffect : KernelEffect
    {
        private readonly string id;
        public int Calls;
        public ProbeEffect(string id) { this.id = id; }
        public override string Id => id;
        public override string Description => "probe";
        public override string LuaExample => "";
        public override void Execute(EffectExecutionContext context, EffectInstruction instruction) { Calls++; }
    }
    [Test] public void EveryLegacyOperationHasExactlyOneIndependentHandler()
    {
        var registry = KernelEffectRegistry.CreateDefault();
        foreach (EffectOp op in Enum.GetValues(typeof(EffectOp)))
        {
            Assert.NotNull(registry.Get(op.ToString()));
            Assert.AreEqual(op + "Effect", registry.Get(op.ToString()).GetType().Name);
            Assert.IsFalse(string.IsNullOrEmpty(registry.Get(op.ToString()).LuaExample));
        }
        Assert.AreEqual(registry.All.Count, registry.All.Select(e => e.Id.ToLowerInvariant()).Distinct().Count());
    }
    [Test] public void CustomStringOperationDoesNotRequireAnEnumOrDispatcherChange()
    {
        var effect = new ProbeEffect("CustomOperation");
        var registry = new KernelEffectRegistry(new[] { effect });
        var instruction = new EffectInstruction { EffectId = "customoperation" };
        registry.Execute(null, instruction, null);
        Assert.AreEqual(1, effect.Calls);
        Assert.AreSame(effect, registry.Get("CustomOperation"));
    }
    [Test] public void DuplicateAndUnknownIdsFailExplicitly()
    {
        Assert.Throws<ArgumentException>(() => new KernelEffectRegistry(new[] { new ProbeEffect("Draw"), new ProbeEffect("draw") }));
        var registry = KernelEffectRegistry.CreateDefault();
        Assert.Throws<FormatException>(() => registry.Execute(null, new EffectInstruction { EffectId = "MissingOperation" }, null));
    }
    [Test] public void ValidationRunsBeforeExecutingHandler()
    {
        var effect = new ProbeEffect("Probe");
        var registry = new KernelEffectRegistry(new[] { effect });
        Assert.Throws<FormatException>(() => registry.Execute(null, new EffectInstruction { EffectId = "Probe", Target = "enemyDeck" }, null));
        Assert.AreEqual(0, effect.Calls);
    }
    [Test] public void IndividualEffectsRejectPrivateChoiceAndInvalidDestinations()
    {
        var registry = KernelEffectRegistry.CreateDefault();
        Assert.Throws<FormatException>(() => registry.Validate(new EffectInstruction { Op = EffectOp.Choose, Zone = TestCardZone.Hand, Side = "opponent" }, null));
        Assert.Throws<FormatException>(() => registry.Validate(new EffectInstruction { Op = EffectOp.Move, Zone = TestCardZone.Deck }, null));
        Assert.Throws<FormatException>(() => registry.Validate(new EffectInstruction { Op = EffectOp.Draw, Amount = -1 }, null));
        Assert.Throws<FormatException>(() => registry.Validate(new EffectInstruction { Op = EffectOp.Heal, Side = "any" }, null));
        Assert.Throws<FormatException>(() => registry.Validate(new EffectInstruction { Op = EffectOp.Spawn, DefinitionId = "missing" }, null));
    }
}
