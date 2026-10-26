using System;
using System.Collections.Generic;
using System.Text;
using CDPIUI.Shared.Exceptions.Interface;
using CDPIUI.Shared.PrettyErrorConversionService;

namespace CDPIUI.Shared.Exceptions.Database;

public class DatabaseUserItemRegistrationException : Exception, ICustomException
{
    public DatabaseUserItemRegistrationException() : base() { }
    public DatabaseUserItemRegistrationException(string message) : base(message) { }
    public DatabaseUserItemRegistrationException(string message, Exception inner) : base(message, inner) { }

    protected DatabaseUserItemRegistrationException(System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
    { }

    public PrettyErrorCode PrettyErrorCode { get => PrettyErrorCode.USER_ITEM_REGISTER; }
}
