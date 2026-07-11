namespace TechcronossTranslationLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--enable-translation", StringComparison.OrdinalIgnoreCase))
        {
            ModInstaller.Apply(true);
            return;
        }
        if (args.Length == 1 && args[0].Equals("--disable-translation", StringComparison.OrdinalIgnoreCase))
        {
            ModInstaller.Apply(false);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
