using System.Diagnostics;
using System.Text;
using ScraperDemo;

Console.OutputEncoding = Encoding.UTF8;

if (!CliOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.WriteLine(CliOptions.Usage);
    return 1;
}
if (options.ShowHelp)
{
    Console.WriteLine(CliOptions.Usage);
    return 0;
}

// Ctrl+C：停止擷取，但仍把已抓到的資料存檔
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("收到中斷指令，停止擷取並儲存已抓到的資料…");
};

var books = new List<Book>();
var stopwatch = Stopwatch.StartNew();
using var client = new PoliteHttpClient();

Console.WriteLine($"開始擷取 {CliOptions.StartUrl}");
Console.WriteLine($"頁數上限：{(options.MaxPages?.ToString() ?? "全部")}｜詳細頁庫存：{(options.FetchDetails ? "是" : "否")}｜請求間隔 1 秒");
Console.WriteLine();

try
{
    string? url = CliOptions.StartUrl;
    var page = 0;
    while (url is not null && (options.MaxPages is null || page < options.MaxPages))
    {
        page++;
        var html = await client.GetStringAsync(url, cts.Token);
        var (pageBooks, next) = await BookPageParser.ParseListPageAsync(html, url);
        books.AddRange(pageBooks);
        Console.WriteLine($"[第 {page,2} 頁] 取得 {pageBooks.Count} 本，累計 {books.Count} 本");
        url = next;
    }

    if (options.FetchDetails)
    {
        Console.WriteLine();
        Console.WriteLine($"抓取 {books.Count} 本書的詳細頁庫存（約需 {books.Count} 秒）…");
        for (var i = 0; i < books.Count; i++)
        {
            var html = await client.GetStringAsync(books[i].Url, cts.Token);
            books[i] = books[i] with { StockCount = await BookPageParser.ParseStockCountAsync(html, books[i].Url) };
            if ((i + 1) % 20 == 0 || i == books.Count - 1)
                Console.WriteLine($"  詳細頁 {i + 1}/{books.Count}");
        }
    }
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    // 使用者中斷，繼續往下存檔
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"擷取失敗（已重試 3 次）：{ex.Message}");
    if (books.Count == 0) return 2;
    Console.WriteLine("仍會儲存已抓到的資料。");
}

if (books.Count == 0)
{
    Console.WriteLine("沒有擷取到任何資料，不產生 Excel。");
    return 2;
}

var outputPath = Path.GetFullPath(options.OutputPath);
ExcelExporter.Export(books, outputPath, options.FetchDetails, DateTime.Now);

Console.WriteLine();
Console.WriteLine($"完成：{books.Count} 本書，耗時 {stopwatch.Elapsed:mm\\:ss}");
Console.WriteLine($"輸出檔案：{outputPath}");
return 0;

/// <summary>命令列參數。</summary>
internal sealed record CliOptions(int? MaxPages, bool FetchDetails, string OutputPath, bool ShowHelp)
{
    public const string StartUrl = "https://books.toscrape.com/catalogue/page-1.html";

    public const string Usage = """
        用法：ScraperDemo [選項]

          --pages <N>      只抓前 N 頁（每頁 20 本，預設抓全部 50 頁）
          --details        逐本開詳細頁抓「庫存數量」（每本 1 秒，全部約 17 分鐘）
          --output <檔名>  輸出檔名，預設 books.xlsx
          -h, --help       顯示說明

        範例：
          ScraperDemo --pages 3
          ScraperDemo --pages 2 --details --output sample.xlsx
        """;

    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions(null, false, "books.xlsx", false);
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pages" when i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n > 0:
                    options = options with { MaxPages = n };
                    i++;
                    break;
                case "--details":
                    options = options with { FetchDetails = true };
                    break;
                case "--output" when i + 1 < args.Length:
                    options = options with { OutputPath = args[++i] };
                    break;
                case "-h" or "--help":
                    options = options with { ShowHelp = true };
                    break;
                default:
                    error = $"無法辨識的參數：{args[i]}";
                    return false;
            }
        }
        return true;
    }
}
