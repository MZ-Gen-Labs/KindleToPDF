namespace KindleToPDF;

using System.IO;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        catch (Exception ex)
        {
            try { File.WriteAllText("startup_error.txt", ex.ToString()); } catch { }
            MessageBox.Show($"An error occurred during startup:\n\n{ex}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}