namespace CDPIUI.ViewModels.Troubleshooting;

/// <summary>What <see cref="WorkViewModel"/> needs from the troubleshooting window.</summary>
public interface IWorkView
{
    /// <summary>Makes the troubleshooting window modal while a check or a fix runs.</summary>
    void MakeWindowModal();

    void MakeWindowNormal();
}
