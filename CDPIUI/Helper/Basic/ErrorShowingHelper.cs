using CDPIUI.Core.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.Helper.Basic
{
    /// <summary>
    /// Shows some error messages
    /// </summary>
    public class ErrorShowingHelper
    {
        /// <summary>
        /// Raise critical exception. That show modal error window, after closing of which
        /// exiting from GUI (CDPUI)
        /// </summary>
        /// <param name="sender">Who called this method</param>
        /// <param name="errorCode">Error code 
        /// (use <see cref="CDPIUI.Shared.PrettyErrorConvertionService.PrettyErrorCode"/>
        /// as <see cref="string"/> instead of custom error)
        /// </param>
        /// <param name="why">Why this happens</param>
        public static void RaiseCriticalException(string sender, string errorCode, string why)
        {
            Logger.Instance.CreateErrorLog(sender, $"{errorCode} => {why}");
            ShowWindow(sender, $"Because exception happens \n{why}", errorCode);
        }

        /// <summary>
        /// Raise critical exception. That show modal error window, after closing of which
        /// exiting from GUI (CDPUI)
        /// </summary>
        /// <param name="sender">Who called this method</param>
        /// <param name="exception">Exception</param>
        public static void RaiseCriticalException(string sender, System.Exception exception)
        {
            string readyToUseErrorCode = ErrorsHelper.Convertor.GetPrettyErrorCode("ERR_INTERNAL", exception);

            Logger.Instance.CreateErrorLog(sender, $"{readyToUseErrorCode} => {exception}");
            ShowWindow(sender, $"Because exception happens \n{exception}", readyToUseErrorCode);
        }

        // Core errors (a lost tray, for example) are raised on pool threads, where creating the
        // window failed and the user never saw the error.
        private static void ShowWindow(string sender, string why, string errorCode)
        {
            void Show()
            {
                CriticalErrorHandlerWindow window = new(where: sender, why: why, errorCode: errorCode);
                window.Activate();
            }

            var dispatcher = (Microsoft.UI.Xaml.Application.Current as App)?.UiDispatcher;
            if (dispatcher == null || dispatcher.HasThreadAccess)
                Show();
            else if (!dispatcher.TryEnqueue(Show))
                Logger.Instance.CreateErrorLog(nameof(ErrorShowingHelper), $"Cannot show the critical error window for {errorCode}");
        }
    }
}
