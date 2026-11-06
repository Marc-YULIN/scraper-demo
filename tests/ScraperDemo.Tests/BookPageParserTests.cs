namespace ScraperDemo.Tests;

public class BookPageParserTests
{
    // 依目標網站的 HTML 結構手寫的精簡範例
    private const string ListPageHtml = """
        <html><body>
        <ol class="row">
          <li><article class="product_pod">
            <p class="star-rating Three"></p>
            <h3><a href="a-very-long-book-title_1000/index.html" title="A Very Long Book Title That Gets Cut">A Very Long ...</a></h3>
            <div class="product_price">
              <p class="price_color">£51.77</p>
              <p class="instock availability">
                  <i class="icon-ok"></i>
                  In stock
              </p>
            </div>
          </article></li>
          <li><article class="product_pod">
            <p class="star-rating Five"></p>
            <h3><a href="short-title_999/index.html" title="Short Title">Short Title</a></h3>
            <div class="product_price">
              <p class="price_color">Â£9.50</p>
              <p class="instock availability">In stock</p>
            </div>
          </article></li>
        </ol>
        <ul class="pager"><li class="next"><a href="page-3.html">next</a></li></ul>
        </body></html>
        """;

    private const string PageUrl = "https://books.toscrape.com/catalogue/page-2.html";

    [Fact]
    public async Task ParseListPage_ReadsAllFields()
    {
        var (books, _) = await BookPageParser.ParseListPageAsync(ListPageHtml, PageUrl);

        Assert.Equal(2, books.Count);
        var first = books[0];
        Assert.Equal("A Very Long Book Title That Gets Cut", first.Title);
        Assert.Equal(51.77m, first.Price);
        Assert.Equal(3, first.Rating);
        Assert.Equal("In stock", first.Availability);
        Assert.Null(first.StockCount);
        Assert.Equal("https://books.toscrape.com/catalogue/a-very-long-book-title_1000/index.html", first.Url);
    }

    [Fact]
    public async Task ParseListPage_HandlesMojibakePoundSign()
    {
        var (books, _) = await BookPageParser.ParseListPageAsync(ListPageHtml, PageUrl);
        Assert.Equal(9.50m, books[1].Price);
        Assert.Equal(5, books[1].Rating);
    }

    [Fact]
    public async Task ParseListPage_ResolvesNextPageToAbsoluteUrl()
    {
        var (_, next) = await BookPageParser.ParseListPageAsync(ListPageHtml, PageUrl);
        Assert.Equal("https://books.toscrape.com/catalogue/page-3.html", next);
    }

    [Fact]
    public async Task ParseListPage_LastPageHasNoNext()
    {
        var html = "<html><body><ul class=\"pager\"><li class=\"previous\"><a href=\"page-49.html\">previous</a></li></ul></body></html>";
        var (books, next) = await BookPageParser.ParseListPageAsync(html, PageUrl);
        Assert.Empty(books);
        Assert.Null(next);
    }

    [Theory]
    [InlineData("In stock (22 available)", 22)]
    [InlineData("In stock (1 available)", 1)]
    [InlineData("Out of stock", null)]
    public async Task ParseStockCount(string availabilityText, int? expected)
    {
        var html = $"""
            <div class="product_main">
              <h1>Book</h1>
              <p class="instock availability"><i class="icon-ok"></i>
                {availabilityText}
              </p>
            </div>
            """;
        Assert.Equal(expected, await BookPageParser.ParseStockCountAsync(html, PageUrl));
    }

    [Theory]
    [InlineData("£1,234.50", 1234.50)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void ParsePrice(string? text, double expected)
    {
        Assert.Equal((decimal)expected, BookPageParser.ParsePrice(text));
    }
}
