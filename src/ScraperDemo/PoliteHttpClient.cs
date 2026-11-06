using System.Diagnostics;
using System.Net;
using System.Text;

namespace ScraperDemo;

/// <summary>
/// 「有禮貌」的 HTTP 用戶端：
/// 每次請求至少間隔 <see cref="MinInterval"/>、帶固定 User-Agent、失敗自動重試。
/// </summary>
public sealed class PoliteHttpClient : IDisposable
{
    public const string UserAgent = "ScraperDemo/1.0 (.NET portfolio demo)";

    private readonly HttpClient _http;
    private readonly Stopwatch _sinceLastRequest = new();

    public TimeSpan MinInterval { get; init; } = TimeSpan.FromSeconds(1);
    public int MaxRetries { get; init; } = 3;

    /// <summary>第一次重試前的等待時間，之後每次加倍（2、4、8 秒）。</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>重試或等待時的訊息輸出，預設寫到主控台。</summary>
    public Action<string> Log { get; init; } = Console.WriteLine;

    public PoliteHttpClient(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            await WaitForTurnAsync(ct);
            try
            {
                using var response = await _http.GetAsync(url, ct);
                if (IsTransient(response.StatusCode) && attempt < MaxRetries)
                {
                    await BackoffAsync(attempt, $"HTTP {(int)response.StatusCode}", url, ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                // 這個網站的回應標頭沒有標 charset，直接以 UTF-8 解碼避免「Â£」亂碼
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex, ct))
            {
                await BackoffAsync(attempt, ex.GetType().Name, url, ct);
            }
        }
    }

    private async Task WaitForTurnAsync(CancellationToken ct)
    {
        if (_sinceLastRequest.IsRunning && _sinceLastRequest.Elapsed < MinInterval)
            await Task.Delay(MinInterval - _sinceLastRequest.Elapsed, ct);
        _sinceLastRequest.Restart();
    }

    private Task BackoffAsync(int attempt, string reason, string url, CancellationToken ct)
    {
        // 指數退避：base、base×2、base×4
        var delay = RetryBaseDelay * Math.Pow(2, attempt);
        Log($"  ! {reason}，{delay.TotalSeconds:0.#} 秒後重試（第 {attempt + 1}/{MaxRetries} 次）：{url}");
        return Task.Delay(delay, ct);
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code == HttpStatusCode.RequestTimeout ||
        code == HttpStatusCode.TooManyRequests ||
        (int)code >= 500;

    private static bool IsRetryable(Exception ex, CancellationToken ct) =>
        // 只重試連線層錯誤；有狀態碼的（如 404）已在上面判斷過，不重試
        ex is HttpRequestException { StatusCode: null } ||
        // HttpClient 逾時會丟 TaskCanceledException；使用者按 Ctrl+C 則不重試
        (ex is TaskCanceledException && !ct.IsCancellationRequested);

    public void Dispose() => _http.Dispose();
}
