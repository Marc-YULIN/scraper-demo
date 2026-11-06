using System.Diagnostics;
using System.Net;

namespace ScraperDemo.Tests;

public class PoliteHttpClientTests
{
    /// <summary>依序回傳預先排好的狀態碼，並記錄收到的請求。</summary>
    private sealed class FakeHandler(params HttpStatusCode[] codes) : HttpMessageHandler
    {
        private int _index;
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var code = codes[Math.Min(_index++, codes.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent("ok") });
        }
    }

    private static PoliteHttpClient CreateClient(FakeHandler handler, TimeSpan? interval = null) =>
        new(handler)
        {
            MinInterval = interval ?? TimeSpan.Zero,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
            Log = _ => { },
        };

    [Fact]
    public async Task SendsUserAgent()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        using var client = CreateClient(handler);

        await client.GetStringAsync("https://example.test/");

        Assert.Equal(PoliteHttpClient.UserAgent, handler.Requests[0].Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task RetriesTransientErrorsThenSucceeds()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError, HttpStatusCode.OK);
        using var client = CreateClient(handler);

        Assert.Equal("ok", await client.GetStringAsync("https://example.test/"));
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task GivesUpAfterThreeRetries()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable);
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync("https://example.test/"));
        Assert.Equal(4, handler.Requests.Count); // 第 1 次 + 重試 3 次
    }

    [Fact]
    public async Task DoesNotRetryNotFound()
    {
        var handler = new FakeHandler(HttpStatusCode.NotFound);
        using var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync("https://example.test/"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task WaitsMinIntervalBetweenRequests()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        using var client = CreateClient(handler, TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        await client.GetStringAsync("https://example.test/1");
        await client.GetStringAsync("https://example.test/2");
        await client.GetStringAsync("https://example.test/3");

        // 3 次請求之間有 2 段間隔；留一點計時誤差
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(580), $"實際耗時 {sw.ElapsedMilliseconds} ms");
    }
}
