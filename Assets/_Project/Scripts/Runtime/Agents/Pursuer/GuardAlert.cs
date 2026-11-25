namespace Lockdown.Agents.Pursuer
{
    /// <summary>
    /// What the guard is warning the thief about. The brain picks one; the presentation owns the wording.
    /// Values are ordered by urgency, so a later value outranks an earlier one.
    /// </summary>
    public enum GuardAlert { None, Approaching, BlockingEscape, AdvancingThroughCover, ReturningFire, Handcuffing }

    public static class GuardAlerts
    {
        public static string Message(GuardAlert alert)
        {
            switch (alert)
            {
                case GuardAlert.Approaching: return "Museum guard approaching — run to avoid arrest";
                case GuardAlert.BlockingEscape: return "Guard is cutting off your escape";
                case GuardAlert.AdvancingThroughCover: return "Guard advancing through cover";
                case GuardAlert.ReturningFire: return "Guard returning fire — break line of sight!";
                case GuardAlert.Handcuffing: return "Handcuffing — move away to escape!";
                default: return null;
            }
        }
    }
}
