using System;
using System.Linq;
using Mishi.Battle;
using NUnit.Framework;

public sealed class EffectVariableTests
{
    [Test] public void TypesAreStableUntilExplicitlyCleared()
    {
        var store = new EffectVariableStore();
        store.Set("counter", EffectValue.Integer(2), 0, false);
        store.Add("counter", EffectValue.Integer(3));
        Assert.AreEqual(5, store.Read("counter").RequireInteger());
        Assert.Throws<FormatException>(() => store.Set("counter", EffectValue.String("five"), 0, false));
        Assert.Throws<FormatException>(() => store.Add("counter", EffectValue.Decimal(.5)));
        Assert.AreEqual(5, store.Read("counter").RequireInteger());
        store.Clear("counter"); store.Set("counter", EffectValue.Bool(false), 0, false);
        Assert.IsFalse(store.Read("counter").Boolean);
        Assert.Throws<FormatException>(() => store.Read("counter").RequireInteger());
        store.Set("ratio", EffectValue.Decimal(1.5), 0, false); store.Add("ratio", EffectValue.Decimal(.25));
        Assert.AreEqual(1.75, store.Read("ratio").Number);
    }
    [Test] public void VisibilityMissingKeysAndNumericBoundsAreExplicit()
    {
        var store = new EffectVariableStore();
        store.Set("secret", EffectValue.String("hidden"), 0, false);
        store.Set("publicFlag", EffectValue.Bool(true), 0, true);
        Assert.AreEqual(2, store.VisibleTo(0).Length);
        Assert.AreEqual("publicFlag", store.VisibleTo(1).Single().Key);
        Assert.Throws<FormatException>(() => store.Read("missing"));
        Assert.Throws<FormatException>(() => EffectValue.Decimal(double.NaN));
        Assert.Throws<FormatException>(() => EffectValue.String(new string('x', 1025)));
        store.Set("max", EffectValue.Integer(1000000000), 0, false);
        Assert.Throws<FormatException>(() => store.Add("max", EffectValue.Integer(1)));
        Assert.AreEqual(1000000000, store.Read("max").RequireInteger());
    }
}
