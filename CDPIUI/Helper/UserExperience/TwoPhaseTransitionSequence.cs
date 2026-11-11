using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CDPIUI.Helper.UserExperience;

/// <summary>Applies two live groups of transitions with a pause between consecutive changes.</summary>
internal static class TwoPhaseTransitionSequence
{
    public static async Task RunAsync<TItem, TState>(IReadOnlyList<(TItem Item, TState State)> first,
        IReadOnlyList<(TItem Item, TState State)> second, Func<TItem, bool> inspect,
        Action<TItem, TState> apply, Func<bool, TimeSpan> firstDelay, Func<bool, TimeSpan> secondDelay,
        Func<TimeSpan, CancellationToken, Task> delay, CancellationTokenSource cancellation, Action onFinally)
    {
        try
        {
            int transitionCount = first.Count + second.Count;
            int transitionIndex = 0;
            foreach ((TItem item, TState state) in first)
            {
                bool before = inspect(item);
                apply(item, state);
                transitionIndex++;
                if (transitionIndex < transitionCount)
                {
                    await delay(firstDelay(before), cancellation.Token);
                }
            }
            foreach ((TItem item, TState state) in second)
            {
                bool before = inspect(item);
                apply(item, state);
                transitionIndex++;
                if (transitionIndex < transitionCount)
                {
                    await delay(secondDelay(before), cancellation.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Replacement or suspension canceled the pending sequence.
        }
        finally
        {
            onFinally();
        }
    }
}
