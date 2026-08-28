using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

internal enum NResult : int
{
    NL_OK = 0,
    NL_INTERNAL_ERROR = unchecked((int)0x80000001),
    NL_NOT_INITIALIZED,
    NL_INVALID_ARGS,
    NL_WAITING_SERVER,
    NL_NO_LOGIN,
    NL_NO_LICENSE,
    NL_PASSWORD_HASH_SHA1,
    NL_PASSWORD_HASH_MD5,
    NL_OUT_OF_RANGE,
    NL_MARKET_ONLY,
    NL_NO_POSITION,
    NL_NOT_FOUND,
    NL_VERSION_NOT_SUPPORTED,
    NL_OCO_NO_RULES,
    NL_EXCHANGE_UNKNOWN,
    NL_NO_OCO_DEFINED,
    NL_INVALID_SERIE,
    NL_LICENSE_NOT_ALLOWED,
    NL_NOT_HARD_LOGOUT,
    NL_SERIE_NO_HISTORY,
    NL_ASSET_NO_DATA,
    NL_SERIE_NO_DATA,
    NL_HAS_STRATEGY_RUNNING,
    NL_SERIE_NO_MORE_HISTORY,
    NL_SERIE_MAX_COUNT,
    NL_DUPLICATE_RESOURCE,
    NL_UNSIGNED_CONTRACT,
    NL_NO_PASSWORD,
    NL_NO_USER,
    NL_FILE_ALREADY_EXISTS,
    NL_INVALID_TICKER,
    NL_NOT_MASTER_ACCOUNT
}

[StructLayout(LayoutKind.Sequential)]
internal struct TAssetID
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Ticker;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string Exchange;

    public int Feed;
}
