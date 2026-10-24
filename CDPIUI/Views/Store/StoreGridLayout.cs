using System;

namespace CDPIUI.Views.Store;

/// <summary>
/// Calculates the item width of the Store home page grid from the available width.
/// </summary>
internal static class StoreGridLayout
{
    /// <summary>
    /// Returns the width of a large item. Each item takes 15 px of margin on both sides,
    /// and a column count above two is kept even.
    /// </summary>
    public static double CalculateLargeElementWidth(double totalWidth, double minimumWidth, double preferredWidth)
    {
        int availableColumns = (int)Math.Round(totalWidth / (minimumWidth + 15 + 15));

        double preCalcWidth = totalWidth / availableColumns;

        if (preCalcWidth < minimumWidth && availableColumns >= 2)
        {
            availableColumns--;
        }
        else if (preCalcWidth > preferredWidth * 1.5)
        {
            if (availableColumns % 2 == 0)
                availableColumns += 2;
            else
                availableColumns++;
        }

        if (availableColumns % 2 != 0 && availableColumns > 2)
        {
            availableColumns--;
        }

        return totalWidth / availableColumns;
    }
}
