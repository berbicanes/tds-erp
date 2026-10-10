using Microsoft.EntityFrameworkCore;
using Tds.Api.Domain;

namespace Tds.Api.Data;

/// <summary>Adds M-01…M-04 if their code does not exist yet. Never changes existing rows. Runs in every environment.</summary>
public sealed class EquipmentSeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await db.Equipment.Select(e => e.Code).ToListAsync(ct);
        foreach (var (code, name) in Equipment.SeedMachines.Where(m => !existing.Contains(m.Code)))
            db.Equipment.Add(new Equipment { Code = code, Name = name, Kind = EquipmentKind.Masina });
        await db.SaveChangesAsync(ct);
    }
}
