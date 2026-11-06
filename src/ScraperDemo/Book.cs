namespace ScraperDemo;

/// <summary>一本書的擷取結果。</summary>
/// <param name="Title">書名</param>
/// <param name="Price">價格（英鎊）</param>
/// <param name="Rating">評分 1–5 星，無法辨識時為 0</param>
/// <param name="Availability">庫存狀態文字，例如 "In stock"</param>
/// <param name="StockCount">庫存數量；只有開啟 --details 時才會抓</param>
/// <param name="Url">書籍詳細頁網址</param>
public sealed record Book(
    string Title,
    decimal Price,
    int Rating,
    string Availability,
    int? StockCount,
    string Url);
