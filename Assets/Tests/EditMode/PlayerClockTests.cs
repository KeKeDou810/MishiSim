using Mishi.Battle;
using NUnit.Framework;

public sealed class PlayerClockTests
{
    [Test] public void ExactPaymentShowsTwelveButNextVoluntaryPaymentIsRejected()
    {
        var clock = new PlayerClock();
        Assert.AreEqual(4, clock.CostPointer); Assert.AreEqual(4, clock.DamagePointer);
        Assert.IsTrue(clock.Pay(4)); Assert.AreEqual(12, clock.CostPointer);
        Assert.IsFalse(clock.Lost); Assert.IsTrue(clock.Pay(0));
        Assert.IsFalse(clock.Pay(1)); Assert.IsFalse(clock.Lost);
        Assert.AreEqual(12, clock.CostPointer); Assert.AreEqual(0, clock.RemainingTime);
    }
    [Test] public void InsufficientVoluntaryPaymentDoesNotChangeState()
    {
        var clock = new PlayerClock(); Assert.IsFalse(clock.Pay(5));
        Assert.AreEqual(4, clock.CostPointer); Assert.AreEqual(4, clock.DamagePointer);
        Assert.IsFalse(clock.Lost); Assert.IsNull(clock.LossReason);
    }
    [Test] public void ForcedOverpaymentLosesAndDamageCannotRevive()
    {
        var clock = new PlayerClock(); Assert.IsFalse(clock.ForcePay(5));
        clock.TakeDamage(1); clock.ReconstructTime();
        Assert.IsTrue(clock.Lost); Assert.AreEqual(12, clock.CostPointer);
    }
    [Test] public void DamageMovesBothPointersIncludingCostWrap()
    {
        var clock = new PlayerClock(); clock.Pay(4); clock.TakeDamage(1);
        Assert.AreEqual(1, clock.CostPointer); Assert.AreEqual(5, clock.DamagePointer);
        clock.ReconstructTime(); Assert.AreEqual(5, clock.CostPointer);
    }
    [Test] public void EightDamageLosesAtTwelveAndDeckDamageDoesNotMoveCost()
    {
        var clock = new PlayerClock(); clock.TakeDamage(2, false);
        Assert.AreEqual(6, clock.DamagePointer); Assert.AreEqual(4, clock.CostPointer);
        clock.TakeDamage(6); Assert.AreEqual(12, clock.DamagePointer); Assert.IsTrue(clock.Lost);
    }
}
