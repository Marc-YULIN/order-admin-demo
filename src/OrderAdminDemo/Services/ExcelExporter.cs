using ClosedXML.Excel;

namespace OrderAdminDemo.Services;

public record ExcelColumn<T>(string Header, Func<T, object?> Value, string? Format = null);

/// <summary>把任一列表匯出成格式化的 Excel（標題列凍結、可篩選、欄寬依中文字寬調整）。</summary>
public static class ExcelExporter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Export<T>(string sheetName, IReadOnlyList<ExcelColumn<T>> columns, IEnumerable<T> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);

        for (var c = 0; c < columns.Count; c++)
            ws.Cell(1, c + 1).Value = columns[c].Header;
        var header = ws.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(r, c + 1);
                cell.Value = XLCellValue.FromObject(columns[c].Value(row));
                if (columns[c].Format is { } f) cell.Style.NumberFormat.Format = f;
            }
            r++;
        }

        ws.Range(1, 1, Math.Max(r - 1, 1), columns.Count).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        FitColumns(ws, columns.Count);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>ClosedXML 的 AdjustToContents 會把中文估太窄，所以自己算：全形字算 2、半形字算 1。</summary>
    private static void FitColumns(IXLWorksheet ws, int columnCount)
    {
        for (var c = 1; c <= columnCount; c++)
        {
            var widest = ws.Column(c).CellsUsed()
                .Select(cell => DisplayWidth(cell.GetFormattedString()))
                .DefaultIfEmpty(0)
                .Max();
            ws.Column(c).Width = Math.Clamp(widest + 3, 8, 50);
        }
    }

    public static int DisplayWidth(string text) => text.Sum(ch => ch >= '⺀' ? 2 : 1);
}
