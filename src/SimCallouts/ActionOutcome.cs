namespace SimCallouts
{
    /// <summary>Result of one action (import/briefing/save/...), shared between MainForm's UI
    /// click handlers (which show a MessageBox on failure - see MainForm.ShowIfFailed) and the
    /// web dashboard's API handlers (which return it as JSON - see MainForm.DashboardApi.cs), so
    /// the actual action logic only has to be written once.</summary>
    public sealed record ActionOutcome(bool Success, string Message)
    {
        public static ActionOutcome Ok(string message) => new(true, message);
        public static ActionOutcome Fail(string message) => new(false, message);
    }
}
