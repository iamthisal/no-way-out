namespace Lockdown.Agents.Pursuer
{
    /// <summary>
    /// What the guard is warning the thief about. The brain picks one; the presentation owns the wording.
    /// Values are ordered by urgency, so a later value outranks an earlier one.
    /// </summary>
    public enum GuardAlert { None, Approaching, BlockingEscape, AdvancingThroughCover, ReturningFire, Handcuffing }

    public static class GuardAlerts
    {
        /// <summary>The short tab at the start of the bar: who is acting.</summary>
        public static string Kicker(GuardAlert alert) =>
            alert == GuardAlert.None ? null : alert == GuardAlert.Handcuffing ? "Arrest" : "Guard";

        public static string Message(GuardAlert alert)
        {
            switch (alert)
            {
                case GuardAlert.Approaching: return "Approaching — run to avoid arrest";
                case GuardAlert.BlockingEscape: return "Cutting off your escape";
                case GuardAlert.AdvancingThroughCover: return "Advancing through cover";
                case GuardAlert.ReturningFire: return "Returning fire — break line of sight";
                case GuardAlert.Handcuffing: return "Handcuffing — move away to escape";
                default: return null;
            }
        }
    }
}
