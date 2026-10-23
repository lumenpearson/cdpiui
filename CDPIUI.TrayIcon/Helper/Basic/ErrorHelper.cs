using CDPIUI.Shared.Logger;
using CDPIUI.Shared.PrettyErrorConversionService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CDPIUI.TrayIcon.Helper.Basic
{
    public static class ErrorHelper
    {
        private static CustomErrorConvertor? _convertor;
        private static readonly object _lock = new object();

        public static CustomErrorConvertor Convertor
        {
            get
            {
                lock (_lock)
                {
                    _convertor ??= new CustomErrorConvertor();
                    return _convertor;
                }
            }
        }

    }

    public class CustomErrorConvertor : ErrorConvertor
    {
        public override string GetPrettyErrorCode(string prefix, int hcode, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(prefix, hcode, Logger.Instance);
        }

        public override string GetPrettyErrorCode(string prefix, Exception ex, ILogger? _ = null)
        {
            return base.GetPrettyErrorCode(prefix, ex, Logger.Instance);
        }

        public PrettyErrorCode GetLoggedErrorCode(string prefix, Exception ex)
        {
            GetPrettyErrorCode(prefix, ex);
            return MapExceptionToCode(ex, out _);
        }
    }
}
