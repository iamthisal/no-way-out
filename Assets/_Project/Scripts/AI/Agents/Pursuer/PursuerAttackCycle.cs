using System;
namespace Lockdown.AI.Agents
{
    public sealed class PursuerAttackCycle
    {
        private readonly PursuerSettings settings;
        private float fireAt, cooldownUntil;
        private int remaining;
        public bool IsAiming { get; private set; }
        public PursuerAttackCycle(PursuerSettings value) { settings = value; }
        public void Tick(float now, bool allowed, Func<bool> fire)
        {
            if (!allowed) { Cancel(now); return; }
            if (now < cooldownUntil) return;
            if (!IsAiming)
            { IsAiming = true; remaining = settings.burstSize; fireAt = now + settings.aimWarning; }
            if (now < fireAt) return;
            if (!fire()) { Cancel(now); return; }
            remaining--;
            if (remaining == 0)
            { IsAiming = false; cooldownUntil = now + settings.burstCooldown; }
            else fireAt = now + settings.burstInterval;
        }
        public void Cancel(float now)
        {
            if (IsAiming) cooldownUntil = now + settings.burstCooldown;
            IsAiming = false; remaining = 0;
        }
    }
}
