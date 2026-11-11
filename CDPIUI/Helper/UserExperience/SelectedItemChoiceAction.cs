#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Captures a selected item and its choices, then applies the chosen reference.</summary>
internal static class SelectedItemChoiceAction
{
    public static async Task ExecuteAsync<TItem, TChoice>(Func<TItem?> selected,
        Func<TItem, IReadOnlyList<TChoice>> choices, Func<IReadOnlyList<TChoice>, Task<TChoice?>> choose,
        Action<TItem, TChoice> apply) where TItem : class where TChoice : class
    {
        TItem? item = selected();
        if (item == null)
        {
            return;
        }
        TChoice[] availableChoices = choices(item).ToArray();
        if (availableChoices.Length == 0)
        {
            return;
        }
        TChoice selectedChoice = availableChoices[0];
        if (availableChoices.Length > 1)
        {
            TChoice? choice = await choose(availableChoices);
            if (choice == null)
            {
                return;
            }
            selectedChoice = choice;
        }
        apply(item, selectedChoice);
    }
}
