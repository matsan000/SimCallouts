namespace SimCallouts
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            UiStyle.IsDarkMode = true;
            Application.Run(new MainForm());
        }
    }
}
