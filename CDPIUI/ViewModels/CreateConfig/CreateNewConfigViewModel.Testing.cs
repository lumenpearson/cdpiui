using System.Threading.Tasks;

namespace CDPIUI.ViewModels.CreateConfig;

// The test run of the config in the selected component.
public sealed partial class CreateNewConfigViewModel
{
    public string TestButtonText
    {
        get => _testSession.TestButtonText;
        private set => _testSession.TestButtonText = value;
    }

    public string TestButtonGlyph
    {
        get => _testSession.TestButtonGlyph;
        private set => _testSession.TestButtonGlyph = value;
    }

    /// <summary>Stops the test component that kept running after the user left the page.</summary>
    public void StopTestRun() => _testSession.Stop();

    private Task ToggleTestRunAsync() => _testSession.ToggleAsync();
}
