namespace GameEcoBot;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            string msg = e.ExceptionObject?.ToString() ?? "Unknown crash";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash_gameecobot.txt"), msg); } catch {}
            MessageBox.Show(msg, "Crash AppDomain", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        Application.ThreadException += (s, e) =>
        {
            string msg = e.Exception?.ToString() ?? "Unknown thread exception";
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash_gameecobot.txt"), msg); } catch {}
            MessageBox.Show(msg, "Crash Thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        try
        {
            ApplicationConfiguration.Initialize();
            bool startMinimized = args.Length > 0 && args[0].Equals("--minimized", StringComparison.OrdinalIgnoreCase);
            Application.Run(new Form1(startMinimized));
        }
        catch (Exception ex)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crash_gameecobot.txt"), ex.ToString()); } catch {}
            MessageBox.Show(ex.ToString(), "GameEcoBot Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}