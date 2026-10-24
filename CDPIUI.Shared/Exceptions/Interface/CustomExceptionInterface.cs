using System;
using System.Collections.Generic;
using System.Text;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Shared.Exceptions.Interface;

public interface ICustomException
{
    public PrettyErrorCode PrettyErrorCode { get; }
}
