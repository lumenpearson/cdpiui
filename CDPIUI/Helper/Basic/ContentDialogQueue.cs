using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CDPIUI;

/// <summary>
/// Only one <see cref="ContentDialog"/> can be open in a window: ShowAsync throws while another one
/// is shown, which happened when a background event raised a dialog over an open one
/// </summary>
public static class ContentDialogQueue
{
    private static readonly ConditionalWeakTable<XamlRoot, SemaphoreSlim> Gates = new();

    /// <summary>
    /// Shows the dialog once the dialogs opened before it in the same window are closed
    /// </summary>
    public static async Task<ContentDialogResult> ShowQueuedAsync(this ContentDialog dialog)
    {
        if (dialog.XamlRoot == null)
            return await dialog.ShowAsync();

        SemaphoreSlim gate = Gates.GetValue(dialog.XamlRoot, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            gate.Release();
        }
    }
}
