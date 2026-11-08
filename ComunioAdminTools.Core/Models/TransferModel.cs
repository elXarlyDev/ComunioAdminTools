namespace ComunioAdminTools.Core.Models;

public sealed record TransferModel(
    string PlayerName,
    string ClubName,
    string SellerName,
    string BuyerName,
    decimal Price,
    decimal QuotedPrice,
    DateTimeOffset DateChanged);
