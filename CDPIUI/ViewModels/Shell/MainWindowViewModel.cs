using System;
using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;

namespace CDPIUI.ViewModels.Shell;

/// <summary>
/// State of the old main window: which component navigation items are shown and the back
/// navigation state. The window keeps the frame navigation and the menu selection.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly Action _installedComponentsChanged;
    private bool _isZapretInstalled = true;
    private bool _isByeDpiInstalled = true;
    private bool _isGoodbyeDpiInstalled = true;
    private bool _isSpoofDpiInstalled = true;
    private bool _canGoBack = true;
    private bool _isBackButtonVisible;

    /// <param name="installedComponentsChanged">
    /// Called after the installed components were read again, so the window can leave a page
    /// of a removed component.
    /// </param>
    public MainWindowViewModel(Action installedComponentsChanged)
    {
        _installedComponentsChanged = installedComponentsChanged;
        StoreHelper.Instance.ItemRemoved += StoreHelper_ItemsChanged;
        StoreHelper.Instance.ItemActionsStopped += StoreHelper_ItemsChanged;
    }

    public bool IsZapretInstalled
    {
        get => _isZapretInstalled;
        private set => SetProperty(ref _isZapretInstalled, value);
    }

    public bool IsByeDpiInstalled
    {
        get => _isByeDpiInstalled;
        private set => SetProperty(ref _isByeDpiInstalled, value);
    }

    public bool IsGoodbyeDpiInstalled
    {
        get => _isGoodbyeDpiInstalled;
        private set => SetProperty(ref _isGoodbyeDpiInstalled, value);
    }

    public bool IsSpoofDpiInstalled
    {
        get => _isSpoofDpiInstalled;
        private set => SetProperty(ref _isSpoofDpiInstalled, value);
    }

    /// <summary>The navigation view back state; the window sets it after each navigation.</summary>
    public bool CanGoBack
    {
        get => _canGoBack;
        set => SetProperty(ref _canGoBack, value);
    }

    /// <summary>The title bar back button is shown for pages that have no menu item.</summary>
    public bool IsBackButtonVisible
    {
        get => _isBackButtonVisible;
        set => SetProperty(ref _isBackButtonVisible, value);
    }

    /// <summary>Reads which components are installed and notifies the window.</summary>
    public void RefreshInstalledComponents()
    {
        IsZapretInstalled = IsInstalled(Components.Zapret);
        IsByeDpiInstalled = IsInstalled(Components.ByeDPI);
        IsGoodbyeDpiInstalled = IsInstalled(Components.GoodbyeDPI);
        IsSpoofDpiInstalled = IsInstalled(Components.SpoofDPI);
        _installedComponentsChanged();
    }

    public void Dispose()
    {
        // StoreHelper outlives the window; each reopened window added handlers.
        StoreHelper.Instance.ItemRemoved -= StoreHelper_ItemsChanged;
        StoreHelper.Instance.ItemActionsStopped -= StoreHelper_ItemsChanged;
    }

    private static bool IsInstalled(Components component) =>
        DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.ComponentIds[component]);

    private void StoreHelper_ItemsChanged(string itemId)
    {
        RefreshInstalledComponents();
    }
}
