using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CDPIUI.TrayIcon.Controls;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CDPIUI.TrayIcon.Tests;

public sealed class ModernButtonFontLifetimeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Exit", false, false, false)]
    [InlineData("Выход", false, false, false)]
    [InlineData("Exit", true, true, true)]
    [InlineData("Выход", true, true, true)]
    [InlineData("Exit", true, false, false)]
    [InlineData("Выход", true, false, false)]
    [InlineData("Exit", false, true, false)]
    [InlineData("Выход", false, true, false)]
    [InlineData("Exit", true, false, true)]
    [InlineData("Выход", true, false, true)]
    public void BoldAssignmentsKeepTheActualCaptionDrawable(string caption, bool first, bool second, bool third)
    {
        output.WriteLine(RuntimeInformation.FrameworkDescription);
        WriteAssemblyIdentity(typeof(Control).Assembly);
        WriteAssemblyIdentity(typeof(Font).Assembly);
        WriteAssemblyIdentity(typeof(ModernButtonUserControl).Assembly);

        RunOnStaThread(() =>
        {
            // Construct only this control. Do not run the tray entry point or create a visible window.
            using var button = new ModernButtonUserControl { DisplayText = caption };
            var label = Assert.Single(button.Controls.OfType<Label>(), item => item.Text == caption);
            AssertDrawable(label, caption, bold: false);

            foreach (bool bold in new[] { first, second, third })
            {
                button.Bold = bold;
                Assert.Equal(bold, button.Bold);
                Assert.Equal(caption, button.DisplayText);
                AssertDrawable(label, caption, bold);
            }
        });
    }

    private void WriteAssemblyIdentity(Assembly assembly)
    {
        var file = FileVersionInfo.GetVersionInfo(assembly.Location);
        output.WriteLine($"{assembly.FullName}; file={file.FileVersion}; path={assembly.Location}");
    }

    private static void AssertDrawable(Label label, string caption, bool bold)
    {
        Assert.Equal(caption, label.Text);
        Assert.Equal(bold, label.Font.Bold);
        using var bitmap = new Bitmap(240, 48);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);

        // Font.Style and Font.Equals can use cached values after disposal. Exercise the native font.
        Exception? error = Record.Exception(() =>
        {
            float height = label.Font.GetHeight(graphics);
            Assert.True(float.IsFinite(height) && height > 0);
            graphics.DrawString(caption, label.Font, Brushes.White, new PointF(2, 2));
        });
        Assert.Null(error);

        bool hasText = false;
        for (int y = 0; y < bitmap.Height && !hasText; y++)
        for (int x = 0; x < bitmap.Width && !hasText; x++)
            hasText = bitmap.GetPixel(x, y).ToArgb() != Color.Black.ToArgb();
        Assert.True(hasText, "The selected caption font must render text into the private test bitmap.");
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { error = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "The control test must finish without a message loop.");
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
