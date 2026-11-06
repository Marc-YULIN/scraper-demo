using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace ScraperDemo;

/// <summary>把 books.toscrape.com 的 HTML 解析成資料。</summary>
public static partial class BookPageParser
{
    private static readonly IBrowsingContext Context = BrowsingContext.New(Configuration.Default);

    /// <summary>解析書籍列表頁，回傳本頁書籍與下一頁網址（沒有下一頁時為 null）。</summary>
    public static async Task<(IReadOnlyList<Book> Books, string? NextPageUrl)> ParseListPageAsync(
        string html, string pageUrl)
    {
        using var doc = await OpenAsync(html, pageUrl);

        var books = doc.QuerySelectorAll("article.product_pod")
            .Select(pod =>
            {
                var link = pod.QuerySelector<IHtmlAnchorElement>("h3 > a");
                return new Book(
                    // 列表上的文字會被截斷成「...」，完整書名在 title 屬性
                    Title: link?.GetAttribute("title")?.Trim() ?? link?.TextContent.Trim() ?? "",
                    Price: ParsePrice(pod.QuerySelector(".price_color")?.TextContent),
                    Rating: ParseRating(pod.QuerySelector("p.star-rating")),
                    Availability: Normalize(pod.QuerySelector(".availability")?.TextContent),
                    StockCount: null,
                    Url: link?.Href ?? "");
            })
            .ToList();

        var next = doc.QuerySelector<IHtmlAnchorElement>("li.next > a")?.Href;
        return (books, next);
    }

    /// <summary>從書籍詳細頁取出庫存數量，例如 "In stock (22 available)" → 22。</summary>
    public static async Task<int?> ParseStockCountAsync(string html, string pageUrl)
    {
        using var doc = await OpenAsync(html, pageUrl);
        var text = doc.QuerySelector(".product_main .availability")?.TextContent ?? "";
        var match = StockCountRegex().Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    public static decimal ParsePrice(string? text)
    {
        // "£51.77" → 51.77；只保留數字與小數點，避開貨幣符號與編碼問題
        var digits = new string((text ?? "").Where(c => char.IsAsciiDigit(c) || c == '.').ToArray());
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    public static int ParseRating(IElement? starRating)
    {
        // <p class="star-rating Three"> → 3
        if (starRating is null) return 0;
        foreach (var cls in starRating.ClassList)
        {
            var rating = cls switch
            {
                "One" => 1, "Two" => 2, "Three" => 3, "Four" => 4, "Five" => 5, _ => 0,
            };
            if (rating > 0) return rating;
        }
        return 0;
    }

    private static Task<IDocument> OpenAsync(string html, string url) =>
        // 指定網址，讓相對連結（href）能解析成完整網址
        Context.OpenAsync(req => req.Content(html).Address(url));

    private static string Normalize(string? text) =>
        WhitespaceRegex().Replace(text ?? "", " ").Trim();

    [GeneratedRegex(@"\((\d+)\s+available\)")]
    private static partial Regex StockCountRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
