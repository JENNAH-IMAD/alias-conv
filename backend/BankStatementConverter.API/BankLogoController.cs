using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

[ApiController, Route("api/banks/{id:guid}/logo")]
public class BankLogoController(AppDbContext db) : ControllerBase
{
    public const int MaxBytes = 2 * 1024 * 1024;
    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var logo = await db.Banks.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Logo, x.LogoContentType }).FirstOrDefaultAsync(ct)
            ?? throw new AppException(404, "Banque introuvable.");
        Response.Headers.CacheControl = "no-store";
        return Ok(new { dataUrl = logo.Logo is null ? null : $"data:{logo.LogoContentType};base64,{Convert.ToBase64String(logo.Logo)}" });
    }
    [HttpPut, RequestSizeLimit(MaxBytes + 65536)]
    public async Task<IActionResult> Put(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxBytes) throw new AppException(400, "Choisissez une image PNG ou JPEG de 2 Mo maximum.");
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var png = bytes.Length >= 33 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8);
        var jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
        if (!png && !jpeg) throw new AppException(400, "Format non pris en charge. Utilisez une image PNG ou JPEG.");
        var bank = await db.Banks.FindAsync([id], ct) ?? throw new AppException(404, "Banque introuvable.");
        bank.Logo = bytes; bank.LogoContentType = png ? "image/png" : "image/jpeg"; bank.LogoVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return Ok(new { bank.LogoVersion });
    }
    [HttpDelete]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var bank = await db.Banks.FindAsync([id], ct) ?? throw new AppException(404, "Banque introuvable.");
        bank.Logo = null; bank.LogoContentType = null; bank.LogoVersion = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
