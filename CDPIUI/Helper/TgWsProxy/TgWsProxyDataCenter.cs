namespace CDPIUI.Helper.TgWsProxy;

/// <summary>
/// One "--dc-ip=number:ip" entry of the tg-ws-proxy startup string.
/// </summary>
internal sealed record TgWsProxyDataCenter(string Guid, string Number, string Ip);
