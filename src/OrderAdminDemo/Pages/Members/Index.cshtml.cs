using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderAdminDemo.Data;
using OrderAdminDemo.Services;

namespace OrderAdminDemo.Pages.Members;

public record MemberRow(int Id, string Name, string Phone, string? Email, string? City, DateTime CreatedAt, int OrderCount, int TotalSpent);

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;
    public PagedList<MemberRow> Result { get; private set; } = null!;

    private IQueryable<MemberRow> Query()
    {
        var q = db.Members.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var k = Q.Trim();
            q = q.Where(m => m.Name.Contains(k) || m.Phone.Contains(k) || (m.Email != null && m.Email.Contains(k)));
        }
        return q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new MemberRow(m.Id, m.Name, m.Phone, m.Email, m.City, m.CreatedAt,
                m.Orders.Count(o => o.Status != OrderStatus.Cancelled),
                m.Orders.Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Shipped).Sum(o => o.Total)));
    }

    public async Task OnGetAsync() => Result = await PagedList<MemberRow>.CreateAsync(Query(), PageNo);

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Query().ToListAsync();
        var bytes = ExcelExporter.Export("會員", [
            new ExcelColumn<MemberRow>("編號", r => r.Id),
            new("姓名", r => r.Name),
            new("手機", r => r.Phone),
            new("Email", r => r.Email),
            new("縣市", r => r.City),
            new("加入日期", r => r.CreatedAt, "yyyy/mm/dd"),
            new("訂單數", r => r.OrderCount),
            new("累計消費", r => r.TotalSpent, "#,##0"),
        ], rows);
        return File(bytes, ExcelExporter.ContentType, $"會員_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        // 刪除只開放管理員：頁面上隱藏按鈕之外，伺服器端也要擋
        if (!User.IsInRole(Roles.Admin)) return Forbid();

        var member = await db.Members.Include(m => m.Orders).FirstOrDefaultAsync(m => m.Id == id);
        if (member is null) return NotFound();
        if (member.Orders.Count > 0)
        {
            TempData["Error"] = $"「{member.Name}」有 {member.Orders.Count} 筆訂單，不能刪除（保留交易紀錄）。";
            return RedirectToPage(new { Q, p = PageNo });
        }
        db.Members.Remove(member);
        await db.SaveChangesAsync();
        TempData["Message"] = $"已刪除會員「{member.Name}」。";
        return RedirectToPage(new { Q, p = PageNo });
    }
}
