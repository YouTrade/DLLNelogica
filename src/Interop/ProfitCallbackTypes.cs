using System.Runtime.InteropServices;

namespace DLLNelogica.Interop;

internal delegate void TStateCallback(int stateType, int result);

internal delegate void THistoryCallBack(
    TAssetID assetId,
    int brokerId,
    int quantity,
    int tradedQuantity,
    int leavesQuantity,
    int side,
    double price,
    double stopPrice,
    double averagePrice,
    long profitId,
    [MarshalAs(UnmanagedType.LPWStr)] string? orderType,
    [MarshalAs(UnmanagedType.LPWStr)] string? account,
    [MarshalAs(UnmanagedType.LPWStr)] string? owner,
    [MarshalAs(UnmanagedType.LPWStr)] string? clientOrderId,
    [MarshalAs(UnmanagedType.LPWStr)] string? status,
    [MarshalAs(UnmanagedType.LPWStr)] string? date);

internal delegate void TOrderChangeCallBack(
    TAssetID assetId,
    int brokerId,
    int quantity,
    int tradedQuantity,
    int leavesQuantity,
    int side,
    double price,
    double stopPrice,
    double averagePrice,
    long profitId,
    [MarshalAs(UnmanagedType.LPWStr)] string? orderType,
    [MarshalAs(UnmanagedType.LPWStr)] string? account,
    [MarshalAs(UnmanagedType.LPWStr)] string? owner,
    [MarshalAs(UnmanagedType.LPWStr)] string? clientOrderId,
    [MarshalAs(UnmanagedType.LPWStr)] string? status,
    [MarshalAs(UnmanagedType.LPWStr)] string? date,
    [MarshalAs(UnmanagedType.LPWStr)] string? textMessage);

internal delegate void TAccountCallback(
    int brokerId,
    [MarshalAs(UnmanagedType.LPWStr)] string? brokerName,
    [MarshalAs(UnmanagedType.LPWStr)] string? accountId,
    [MarshalAs(UnmanagedType.LPWStr)] string? ownerName);

internal delegate void TTradeCallback(
    TAssetID assetId,
    [MarshalAs(UnmanagedType.LPWStr)] string? date,
    uint tradeNumber,
    double price,
    double volume,
    int quantity,
    int buyAgent,
    int sellAgent,
    int tradeType,
    int isEdit);

internal delegate void TNewDailyCallback(
    TAssetID assetId,
    [MarshalAs(UnmanagedType.LPWStr)] string? date,
    double open,
    double high,
    double low,
    double close,
    double volume,
    double adjustment,
    double maxLimit,
    double minLimit,
    double volumeBuyer,
    double volumeSeller,
    int quantity,
    int tradesCount,
    int openContracts,
    int quantityBuyer,
    int quantitySeller,
    int tradesBuyer,
    int tradesSeller);

internal delegate void TPriceBookCallback(
    TAssetID assetId,
    int action,
    int position,
    int side,
    int quantity,
    int count,
    double price,
    nint arraySell,
    nint arrayBuy);

internal delegate void TOfferBookCallback(
    TAssetID assetId,
    int action,
    int position,
    int side,
    int quantity,
    int agent,
    long offerId,
    double price,
    ushort hasPrice,
    ushort hasQuantity,
    ushort hasDate,
    ushort hasOfferId,
    ushort hasAgent,
    [MarshalAs(UnmanagedType.LPWStr)] string? date,
    nint arraySell,
    nint arrayBuy);

internal delegate void THistoryTradeCallback(
    TAssetID assetId,
    [MarshalAs(UnmanagedType.LPWStr)] string? date,
    uint tradeNumber,
    double price,
    double volume,
    int quantity,
    int buyAgent,
    int sellAgent,
    int tradeType);

internal delegate void TProgressCallBack(TAssetID assetId, int progress);

internal delegate void TNewTinyBookCallBack(TAssetID assetId, double price, int quantity, int side);
