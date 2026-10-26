using System;
using System.Drawing;
using System.Windows.Forms;

namespace Update
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string extractFileName = null;
            string destinationPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                // A quoted path that ends with a backslash ("C:\") reaches us with a trailing quote.
                if (args[i] == "--directory-to-zip" && i + 1 < args.Length)
                {
                    extractFileName = args[++i].Trim('"');
                }
                else if (args[i] == "--destination-directory" && i + 1 < args.Length)
                {
                    destinationPath = args[++i].Trim('"');
                }
            }
            if (!string.IsNullOrEmpty(extractFileName) && !string.IsNullOrEmpty(destinationPath))
            {
                InstallHelper.Instance.Init(extractFileName, destinationPath, "CDPIUI_TrayIcon.exe");
                

                // The window layout has fixed sizes in pixels. As on .NET Framework without a
                // dpiAware manifest entry, Windows scales the window.
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // .NET 8 uses Segoe UI 9 pt as the default font. The window layout is made for the
                // .NET Framework default font, so keep it.
                Application.SetDefaultFont(new Font("Microsoft Sans Serif", 8.25F));
                Application.Run(new MainWindow());
            }
            else
            {
                MessageBox.Show(Strings.StartedWithoutArgumentsMessage,
                    Strings.StartedWithoutArgumentsTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
