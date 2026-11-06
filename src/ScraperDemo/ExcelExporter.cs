using ClosedXML.Excel;

namespace ScraperDemo;

/// <summary>把擷取結果輸出成格式化的 Excel。</summary>
public static class ExcelExporter
{
    public static void Export(IReadOnlyList<Book> books, string path, bool includeStockCount, DateTime crawledAt)
    {
        using var workbook = new XLWorkbook();
        WriteBooksSheet(workbook.AddWorksheet("書籍清單"), books, includeStockCount);
        WriteSummarySheet(workbook.AddWorksheet("摘要"), books, crawledAt);
        workbook.SaveAs(path);
    }

    private static void WriteBooksSheet(IXLWorksheet ws, IReadOnlyList<Book> books, bool includeStockCount)
    {
        var headers = new List<string> { "#", "書名", "價格 (£)", "評分", "星等", "庫存狀態" };
        if (includeStockCount) headers.Add("庫存數量");
        headers.Add("網址");

        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        for (var i = 0; i < books.Count; i++)
        {
            var b = books[i];
            var row = i + 2;
            var col = 1;
            ws.Cell(row, col++).Value = i + 1;
            ws.Cell(row, col++).Value = b.Title;
            ws.Cell(row, col++).Value = b.Price;
            ws.Cell(row, col++).Value = b.Rating;
            ws.Cell(row, col++).Value = new string('★', b.Rating) + new string('☆', 5 - b.Rating);
            ws.Cell(row, col++).Value = b.Availability;
            if (includeStockCount)
                ws.Cell(row, col++).Value = b.StockCount is int n ? n : Blank.Value;

            var urlCell = ws.Cell(row, col);
            urlCell.Value = b.Url;
            if (Uri.TryCreate(b.Url, UriKind.Absolute, out var uri))
                urlCell.SetHyperlink(new XLHyperlink(uri));
        }

        var lastRow = Math.Max(books.Count + 1, 1);
        var lastCol = headers.Count;

        // 標題列：粗體、底色、置中、凍結、篩選
        var header = ws.Range(1, 1, 1, lastCol);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#305496");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.SheetView.FreezeRows(1);
        if (books.Count > 0)
            ws.Range(1, 1, lastRow, lastCol).SetAutoFilter();

        // 數字格式與對齊
        ws.Column(3).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Column(5).Style.Font.FontColor = XLColor.FromHtml("#C65911");

        ws.Columns(1, lastCol).AdjustToContents();
        foreach (var column in ws.Columns(1, lastCol))
        {
            // 中文標題常被估得太窄，加上篩選箭頭會被遮住，設最小欄寬
            var min = column.ColumnNumber() == 1 ? 6 : 12;
            if (column.Width < min) column.Width = min;
        }
        // 書名、網址太長時限制欄寬，避免整張表被撐開
        if (ws.Column(2).Width > 60) ws.Column(2).Width = 60;
        if (ws.Column(lastCol).Width > 50) ws.Column(lastCol).Width = 50;
    }

    private static void WriteSummarySheet(IXLWorksheet ws, IReadOnlyList<Book> books, DateTime crawledAt)
    {
        var rows = new (string Label, XLCellValue Value, string? Format)[]
        {
            ("擷取時間", crawledAt, "yyyy-mm-dd hh:mm:ss"),
            ("資料來源", "https://books.toscrape.com", null),
            ("書籍總數", books.Count, "#,##0"),
            ("平均價格 (£)", books.Count > 0 ? books.Average(b => b.Price) : 0m, "#,##0.00"),
            ("最高價格 (£)", books.Count > 0 ? books.Max(b => b.Price) : 0m, "#,##0.00"),
            ("最低價格 (£)", books.Count > 0 ? books.Min(b => b.Price) : 0m, "#,##0.00"),
            ("5 星書籍數", books.Count(b => b.Rating == 5), "#,##0"),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var (label, value, format) = rows[i];
            ws.Cell(i + 1, 1).Value = label;
            var cell = ws.Cell(i + 1, 2);
            cell.Value = value;
            if (format is not null) cell.Style.NumberFormat.Format = format;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        ws.Column(1).Style.Font.Bold = true;
        ws.Column(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        ws.Columns(1, 2).AdjustToContents();
        if (ws.Column(1).Width < 16) ws.Column(1).Width = 16;
    }
}
