using System.Threading.Tasks;
using CDPIUI.Core.ComponentServices.Helpers.Configuration;

namespace CDPIUI.ViewModels.ConfigImport;

/// <summary>The parts of the preset import page that only the page can do.</summary>
public interface IConfigImportPageView
{
    bool IsViewLoaded { get; }

    /// <summary>Shows the step with its animation.</summary>
    void GoToStep(ConfigImportStep step);

    /// <summary>Asks for the preset files to import; <see langword="null"/> when the user cancels.</summary>
    string[] PickImportFiles();

    /// <summary>Asks for a file that replaces a missing one; <see langword="null"/> when the user cancels.</summary>
    string PickReplacementFile(string missingPath);

    /// <summary>Offers to install the component a shared preset needs.</summary>
    Task OfferComponentInstallAsync(ConfigItem config);

    Task ShowErrorAsync(string message);
}
