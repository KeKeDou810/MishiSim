using System;

namespace Mishi.Battle
{
    // Cost runs counter-clockwise; damage runs clockwise. Zero cost is displayed as 12.
    public sealed class PlayerClock
    {
        public ClockKind Kind { get; private set; }
        public void SetKind(ClockKind kind)
        {
            if (!Enum.IsDefined(typeof(ClockKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
        }
        public void AdjustCost(int amount, int maximum = 11)
        {
            if (maximum < 0 || maximum > 11) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (!Lost) RemainingTime = (int)Math.Max(0, Math.Min(maximum, (long)RemainingTime + amount));
        }
        public void Heal(int amount, bool moveCost = false, int minimumDamage = 1)
        {
            if (amount < 0 || minimumDamage < 0 || minimumDamage > 11) throw new ArgumentOutOfRangeException();
            if (Lost) return;
            int healed = Math.Min(amount, Math.Max(0, DamagePointer - minimumDamage));
            DamagePointer -= healed;
            if (moveCost) RemainingTime = Math.Max(0, RemainingTime - healed);
        }
        public int RemainingTime { get; private set; } = 4;
        public int CostPointer => RemainingTime == 0 ? 12 : RemainingTime;
        public int DamagePointer { get; private set; } = 4;
        public bool Lost { get; private set; }
        public string LossReason { get; private set; }
        public bool Pay(int time)
        {
            if (time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (Lost) return false;
            if (time > RemainingTime) return false;
            RemainingTime -= time;
            return true;
        }
        // Reserved for mandatory effects; voluntary card actions must use Pay.
        public bool ForcePay(int time)
        {
            if (time < 0) throw new ArgumentOutOfRangeException(nameof(time));
            if (Lost) return false;
            if (time > RemainingTime)
            {
                RemainingTime = 0; Lost = true; LossReason = "费用指针越过 12";
                return false;
            }
            RemainingTime -= time;
            return true;
        }
        public void TakeDamage(int amount, bool moveCost = true)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Lost) return;
            int steps = Math.Min(amount, 12 - DamagePointer);
            DamagePointer += steps;
            if (moveCost) RemainingTime = (RemainingTime + steps) % 12;
            if (DamagePointer >= 12) { Lost = true; LossReason = "伤害指针到达 12"; }
        }
        public void ReconstructTime()
        {
            if (!Lost) RemainingTime = DamagePointer;
        }
    }
}
