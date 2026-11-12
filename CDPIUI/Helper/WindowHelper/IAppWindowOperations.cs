#nullable enable

using System;

namespace CDPIUI.Helper.WindowHelper;

internal interface IAppWindowOperations<TWindow> where TWindow : class
{
    bool HasDispatcher(TWindow window);
    bool TryGetTemplateId(TWindow window, out string? id);
    TWindow Create(Type windowType);
    void SetTemplateId(TWindow window, string id);
    void SetPosition(TWindow window);
    void Register(TWindow window, bool isUnsafe);
    void Activate(TWindow window);
    void Close(TWindow window);
}
