using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.Import;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

/// <summary>ROADMAP Faza 5 import on synthetic Notion CSV exports (Fixtures/Notion). Not yet tried on the client's real data.</summary>
public class NotionImportTests(PostgresFixture postgres)
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Notion", name);

    private static async Task<ImportReport> Import(TdsApiFactory factory, string folder, bool apply)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(Today, new TimeOnly(8, 0), TimeSpan.Zero)));
        return await new NotionImporter(db, clock).RunAsync(folder, apply, Ct);
    }

    private static T Query<T>(TdsApiFactory factory, Func<AppDbContext, T> query) =>
        factory.Service(sp => query(sp.GetRequiredService<AppDbContext>()));

    private static async Task<Guid> AddEmployee(TdsApiFactory factory, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var e = new Employee { FullName = name };
        db.Employees.Add(e);
        await db.SaveChangesAsync(Ct);
        return e.Id;
    }

    private static (string, int?)[] Where(ImportReport report, FindingKind kind) =>
        report.Findings.Where(f => f.Kind == kind).Select(f => (f.Source, f.Line)).Order().ToArray();

    private static void Has(ImportReport report, FindingKind kind, string source, int? line, string text) =>
        Assert.Contains(report.Findings, f => f.Kind == kind && f.Source == source && f.Line == line && f.Message.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task Clean_export_dry_run_writes_nothing_then_apply_imports_everything_and_a_rerun_finds_only_duplicates()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var amra = await AddEmployee(factory, "Amra Hodžić");

        var dry = await Import(factory, Fixture("ok"), apply: false);
        Assert.False(dry.HasErrors, dry.Render(apply: false));
        Assert.False(dry.Applied);
        Assert.Equal(new Dictionary<string, int>
        {
            ["Dobavljači"] = 2, ["Artikli"] = 2, ["Lotovi"] = 3, ["Početno stanje (korekcije)"] = 2,
            ["Nalozi"] = 2, ["Veze lot–nalog (bez rezervacije)"] = 1,
        }, dry.Created);
        Assert.Contains(dry.Files, f => f.Contains("Zalihe materijala 99aa_all.csv (redova: 3)", StringComparison.Ordinal));
        Assert.Equal(0, Query(factory, db => db.Suppliers.Count() + db.MaterialLots.Count() + db.WorkOrders.Count() + db.StockMovements.Count()));

        // Data that is deliberately not imported is reported, never dropped silently.
        Assert.Equal(new (string, int?)[]
        {
            (NotionImporter.Suppliers, 3), (NotionImporter.Orders, 2), (NotionImporter.Orders, 2),
            (NotionImporter.Lots, 2), (NotionImporter.Stock, 3), (NotionImporter.Stock, 4),
        }.Order().ToArray(), Where(dry, FindingKind.Upozorenje));
        Has(dry, FindingKind.Upozorenje, NotionImporter.Stock, 3, "Rezervisano (100) se ne uvozi");
        Has(dry, FindingKind.Upozorenje, NotionImporter.Stock, 4, "stanje je nepoznato");
        Has(dry, FindingKind.Upozorenje, NotionImporter.Orders, 2, "Odradjeno (50) se ne uvozi");
        Has(dry, FindingKind.Upozorenje, NotionImporter.Orders, 2, "Kvačica „Laser — prvi komad potvrđen”");

        var applied = await Import(factory, Fixture("ok"), apply: true);
        Assert.True(applied.Applied, applied.Render(apply: true));

        var celik = Query(factory, db => db.Suppliers.Single(s => s.Name == "Čelik Trade d.o.o."));
        Assert.Equal(SupplierApprovalStatus.Odobren, celik.ApprovalStatus);
        Assert.Equal("Lim S235, inox", celik.MaterialOrService);
        Assert.Equal(new DateOnly(2026, 3, 3), celik.InitialEvaluationDate);
        Assert.Equal(amra, celik.ResponsibleEmployeeId);
        Assert.Null(celik.CreatedBy);
        Assert.Equal("Napomena sa \"navodnicima\"\nu dva reda", Query(factory, db => db.Suppliers.Single(s => s.Name == "Alu Metal").Notes));

        var lim = Query(factory, db => db.MaterialItems.Single(i => i.Name == "Lim crni"));
        Assert.Equal((Unit.kg, 500m, new DateOnly(2026, 12, 31)), (lim.Unit, lim.MinimumStock!.Value, lim.NextStocktakeDate!.Value));

        var lots = Query(factory, db => db.MaterialLots.ToDictionary(l => l.LotNumber));
        Assert.Equal(["AL-77", "H-4471", "H-4472"], lots.Keys.Order());
        Assert.All(lots.Values, l => Assert.Null(l.ReceivedQty));
        Assert.Equal((ReceiptDecision.Prihvacen, amra, lim.Id, celik.Id), (lots["H-4471"].ReceiptDecision, lots["H-4471"].InspectedByEmployeeId, lots["H-4471"].MaterialItemId, lots["H-4471"].SupplierId));
        Assert.Equal(("3000×1500, 10 tabli", new DateOnly(2026, 10, 1)), (lots["H-4471"].DimensionsDescription, lots["H-4471"].ReceivedDate));
        Assert.Equal(ReceiptDecision.NaKontroli, lots["H-4472"].ReceiptDecision);
        Assert.Equal(ReceiptDecision.Karantin, lots["AL-77"].ReceiptDecision);

        // Initial stock: one Korekcija per lot with stock, no Prijem (golden rule 1: stock only from movements).
        var movements = Query(factory, db => db.StockMovements.ToList());
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal((StockMovementType.Korekcija, NotionImporter.InitialStockReason, Today), (m.Type, m.Reason, m.Date)));
        Assert.Equal(1250.5m, movements.Single(m => m.MaterialLotId == lots["H-4471"].Id).Quantity);
        Assert.Equal(300m, movements.Single(m => m.MaterialLotId == lots["H-4472"].Id).Quantity);

        var order = Query(factory, db => db.WorkOrders.Single(w => w.Number == "RN-2026-0007"));
        Assert.Equal((WorkOrderStatus.Laser, Priority.Hitno, QualityApproval.Ceka, 200), (order.Status, order.Priority, order.QualityApproval, order.OrderedQty));
        Assert.Equal((new DateOnly(2026, 11, 15), new DateOnly(2026, 10, 3)), (order.DueDate, order.ReleaseDate!.Value));
        var link = Query(factory, db => db.WorkOrderMaterials.Single());
        Assert.Equal((order.Id, lots["H-4471"].Id, 0m), (link.WorkOrderId, link.MaterialLotId, link.ReservedQty));
        Assert.True(Query(factory, db => db.WorkOrders.Any(w => w.Number == "N-15/2026")));
        // New orders continue after the imported RN-2026-0007.
        Assert.Equal(7, Query(factory, db => db.NumberSequences.Single(n => n.Kind == "RN" && n.Period == "2026").LastValue));
        Assert.True(Query(factory, db => db.AuditLogs.Any(a => a.EntityId == celik.Id && a.UserId == null)));

        var rerun = await Import(factory, Fixture("ok"), apply: true);
        Assert.False(rerun.HasErrors, rerun.Render(apply: true));
        Assert.Empty(rerun.Created);
        Assert.Equal(9, rerun.Findings.Count(f => f.Kind == FindingKind.Duplikat)); // 2 suppliers, 2 items, 3 lots, 2 orders
        Has(rerun, FindingKind.Upozorenje, NotionImporter.Stock, 2, "početno stanje se ne dodaje");
        Assert.Equal(2, Query(factory, db => db.StockMovements.Count()));

        // The same key with other data in the application is a conflict, not an overwrite.
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Suppliers.Where(s => s.Id == celik.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ApprovalStatus, SupplierApprovalStatus.Blokiran), Ct);
        var conflict = await Import(factory, Fixture("ok"), apply: false);
        Assert.Equal(new (string, int?)[] { (NotionImporter.Suppliers, 2) }, Where(conflict, FindingKind.Greska));
        Has(conflict, FindingKind.Greska, NotionImporter.Suppliers, 2, "već postoji u aplikaciji sa drugim podacima (Status odobrenja)");
    }

    [Fact]
    public async Task Problems_are_reported_with_row_numbers_and_nothing_is_written()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);

        var report = await Import(factory, Fixture("problemi"), apply: true);

        Assert.True(report.HasErrors);
        Assert.False(report.Applied);
        Assert.Equal(new (string, int?)[]
        {
            (NotionImporter.Suppliers, 4), (NotionImporter.Suppliers, 5), (NotionImporter.Suppliers, 6),
            (NotionImporter.Stock, 2), (NotionImporter.Stock, 3),
            (NotionImporter.Orders, 3), (NotionImporter.Orders, 4), (NotionImporter.Orders, 5), (NotionImporter.Orders, 6),
        }.Order().ToArray(), Where(report, FindingKind.Greska));
        // Duplicate: same supplier, same data. Conflict: same supplier, other status.
        Assert.Equal(new (string, int?)[] { (NotionImporter.Suppliers, 3) }, Where(report, FindingKind.Duplikat));
        Has(report, FindingKind.Greska, NotionImporter.Suppliers, 4, "Konflikt: dobavljač „Čelik Trade d.o.o.” je već u redu 2 sa drugim podacima (Status odobrenja)");
        // Unknown enum value: listed with the allowed values, never mapped to the closest one.
        Has(report, FindingKind.Greska, NotionImporter.Suppliers, 5, "Status odobrenja: nepoznata vrijednost \"Na čekanju\" (dozvoljeno: U procjeni, Odobren, Uslovno odobren, Blokiran)");
        Has(report, FindingKind.Greska, NotionImporter.Suppliers, 6, "Naziv dobavljača: prazno obavezno polje.");
        Has(report, FindingKind.Greska, NotionImporter.Stock, 2, "Količina na stanju bez lota");
        Has(report, FindingKind.Greska, NotionImporter.Stock, 3, "Konflikt: artikal „Lim crni S235 3 mm” je već u redu 2 sa drugim podacima (Jedinica)");
        Has(report, FindingKind.Greska, NotionImporter.Orders, 3, "Status: nepoznata vrijednost \"Plazma\"");
        Has(report, FindingKind.Greska, NotionImporter.Orders, 4, "Kupac: prazno obavezno polje.");
        Has(report, FindingKind.Greska, NotionImporter.Orders, 5, "Konflikt: Kolicina Narudzbe (10) i Naručena količina (12) se razlikuju.");
        Has(report, FindingKind.Greska, NotionImporter.Orders, 6, "Rok isporuke: \"12/01/2026\" nije datum");
        // Unknown person in an optional field: warning, the supplier would be imported without it.
        Has(report, FindingKind.Upozorenje, NotionImporter.Suppliers, 7, "zaposlenik \"Nepoznata Osoba\" ne postoji u aplikaciji");
        Has(report, FindingKind.Upozorenje, NotionImporter.Lots, null, "Fajl nije pronađen");
        Assert.Contains("Ništa nije upisano.", report.Render(apply: true));

        Assert.Equal(0, Query(factory, db => db.Suppliers.Count() + db.MaterialItems.Count() + db.WorkOrders.Count() + db.StockMovements.Count()));
    }

    [Fact]
    public async Task Missing_folder_and_missing_required_column_are_errors()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var missing = await Import(factory, Fixture("ne-postoji"), apply: false);
        Has(missing, FindingKind.Greska, "Folder", null, "ne postoji");

        var report = new ImportReport();
        var table = NotionCsv.Read("Dobavljači x.csv", "Naziv dobavljača\nČelik\n");
        await using var scope = factory.Services.CreateAsyncScope();
        var importer = new NotionImporter(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<IClock>());
        await importer.RunAsync(new Dictionary<string, CsvTable> { [NotionImporter.Suppliers] = table }, apply: true, report, Ct);
        Has(report, FindingKind.Greska, NotionImporter.Suppliers, null, "Kolona „Status odobrenja” ne postoji");
        Assert.False(report.Applied);
    }
}
