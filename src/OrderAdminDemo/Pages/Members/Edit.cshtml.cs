using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderAdminDemo.Data;

namespace OrderAdminDemo.Pages.Members;

public class MemberInput
{
    [Required(ErrorMessage = "請輸入姓名"), StringLength(30)]
    [Display(Name = "姓名")] public string Name { get; set; } = "";

    [Required(ErrorMessage = "請輸入手機"), RegularExpression(@"^09\d{8}$", ErrorMessage = "手機格式為 09 開頭共 10 碼")]
    [Display(Name = "手機")] public string Phone { get; set; } = "";

    [EmailAddress(ErrorMessage = "Email 格式不正確")]
    [Display(Name = "Email")] public string? Email { get; set; }

    [Display(Name = "縣市")] public string? City { get; set; }

    [StringLength(200)]
    [Display(Name = "備註")] public string? Note { get; set; }
}

public class EditModel(AppDbContext db, TimeProvider clock) : PageModel
{
    [BindProperty] public MemberInput Input { get; set; } = new();
    public int? Id { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        Id = id;
        if (id is null) return Page();
        var m = await db.Members.FindAsync(id);
        if (m is null) return NotFound();
        Input = new MemberInput { Name = m.Name, Phone = m.Phone, Email = m.Email, City = m.City, Note = m.Note };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        Id = id;
        if (!ModelState.IsValid) return Page();

        Member? m;
        if (id is null)
        {
            m = new Member { Name = Input.Name, Phone = Input.Phone, CreatedAt = clock.GetLocalNow().DateTime };
            db.Members.Add(m);
        }
        else
        {
            m = await db.Members.FindAsync(id);
            if (m is null) return NotFound();
        }
        m.Name = Input.Name.Trim();
        m.Phone = Input.Phone.Trim();
        m.Email = Input.Email?.Trim();
        m.City = Input.City;
        m.Note = Input.Note;
        await db.SaveChangesAsync();

        TempData["Message"] = $"已儲存會員「{m.Name}」。";
        return RedirectToPage("Index");
    }
}
