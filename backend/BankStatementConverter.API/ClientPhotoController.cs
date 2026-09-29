using BankStatementConverter.Application;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace BankStatementConverter.API;

[ApiController, Authorize, Route("api/clients/{id:guid}/photo")]
public class ClientPhotoController(AppDbContext db) : ControllerBase
{
    public const int MaxBytes = 2 * 1024 * 1024;
    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var photo = await db.Clients.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Photo, x.PhotoContentType }).FirstOrDefaultAsync(ct)
            ?? throw new AppException(404, "Client introuvable.");
        Response.Headers.CacheControl = "no-store";
        return Ok(new { dataUrl = photo.Photo is null ? null : $"data:{photo.PhotoContentType};base64,{Convert.ToBase64String(photo.Photo)}" });
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
        var client = await db.Clients.FindAsync([id], ct) ?? throw new AppException(404, "Client introuvable.");
        client.Photo = bytes; client.PhotoContentType = png ? "image/png" : "image/jpeg"; client.PhotoVersion = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return Ok(new { client.PhotoVersion });
    }
    [HttpDelete]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var client = await db.Clients.FindAsync([id], ct) ?? throw new AppException(404, "Client introuvable.");
        client.Photo = null; client.PhotoContentType = null; client.PhotoVersion = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
