using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Features.Import;

/// <summary>
/// One-time import of the client's Notion CSV export (ROADMAP Faza 5, mapping in NOTION_SOURCE.md "Import"):
/// suppliers, material items, lots, initial stock (Korekcija) and work orders. All or nothing in one transaction;
/// any error means nothing is written. Unknown values are reported, never guessed.
/// </summary>
public sealed partial class NotionImporter(AppDbContext db, IClock clock)
{
    public const string InitialStockReason = "Početno stanje iz Notiona";
    public const string Suppliers = "Dobavljači";
    public const string Lots = "Ulazni materijal u proizvodnji";
    public const string Stock = "Zalihe materijala";
    public const string Orders = "Nalozi proizvodnje";

    private static readonly Dictionary<string, string[]> RequiredColumns = new()
    {
        [Suppliers] = ["Naziv dobavljača", "Status odobrenja"],
        [Lots] = ["Oznaka lota / heat broj", "Datum prijema", "Odluka prijema"],
        [Stock] = ["Materijal / artikal", "Jedinica", "Ulazni lot", "Količina na stanju"],
        [Orders] = ["Broj naloga", "Kupac", "Proizvod / opis", "Rok isporuke", "Status", "Prioritet", "Odobrenje kvaliteta"],
    };

    // Notion option names exactly as in NOTION_SOURCE.md (compared without case).
    private static readonly Dictionary<string, SupplierApprovalStatus> SupplierStatuses = Map(
        ("U procjeni", SupplierApprovalStatus.UProcjeni), ("Odobren", SupplierApprovalStatus.Odobren),
        ("Uslovno odobren", SupplierApprovalStatus.UslovnoOdobren), ("Blokiran", SupplierApprovalStatus.Blokiran));
    private static readonly Dictionary<string, ReceiptDecision> Decisions = Map(
        ("Na kontroli", ReceiptDecision.NaKontroli), ("Prihvaćen", ReceiptDecision.Prihvacen),
        ("Karantin", ReceiptDecision.Karantin), ("Odbijen", ReceiptDecision.Odbijen));
    private static readonly Dictionary<string, Unit> Units = Map(
        ("kg", Unit.kg), ("kom", Unit.kom), ("m", Unit.m), ("tabla", Unit.tabla), ("drugo", Unit.drugo));
    private static readonly Dictionary<string, WorkOrderStatus> Statuses = Map(
        ("Zaprimljen", WorkOrderStatus.Zaprimljen), ("Čeka materijal", WorkOrderStatus.CekaMaterijal),
        ("Spreman", WorkOrderStatus.Spreman), ("Laser", WorkOrderStatus.Laser), ("Presanje", WorkOrderStatus.Presanje),
        ("Kontrola", WorkOrderStatus.Kontrola), ("Doraditi", WorkOrderStatus.Doraditi), ("Završeno", WorkOrderStatus.Zavrseno),
        ("Isporučeno", WorkOrderStatus.Isporuceno), ("Otkazano", WorkOrderStatus.Otkazano));
    private static readonly Dictionary<string, Priority> Priorities = Map(("Normalan", Priority.Normalan), ("Hitno", Priority.Hitno));
    private static readonly Dictionary<string, QualityApproval> Approvals = Map(
        ("Čeka", QualityApproval.Ceka), ("Prihvaćeno", QualityApproval.Prihvaceno), ("Blokirano", QualityApproval.Blokirano));

    private static readonly string[] QualityCheckboxes =
        ["Laser — prvi komad potvrđen", "Presanje — prvi komad potvrđen", "Završna kontrola prošla", "Spremno za isporuku"];

    /// <summary>Reads the export folder and runs the import. <paramref name="apply"/> = false is the dry run.</summary>
    public async Task<ImportReport> RunAsync(string folder, bool apply, CancellationToken ct)
    {
        var report = new ImportReport();
        var tables = LoadTables(folder, report);
        return await RunAsync(tables, apply, report, ct);
    }

    public async Task<ImportReport> RunAsync(IReadOnlyDictionary<string, CsvTable> tables, bool apply, ImportReport report, CancellationToken ct)
    {
        var usable = new Dictionary<string, CsvTable>();
        foreach (var (name, table) in tables)
        {
            var missing = RequiredColumns[name].Where(c => !table.Headers.Contains(c)).ToList();
            foreach (var c in missing)
                report.Error(name, null, $"Kolona „{c}” ne postoji u fajlu {table.FileName}; ova baza se ne može uvesti.");
            if (missing.Count == 0)
                usable[name] = table;
        }

        var run = new Run(await Existing.LoadAsync(db, ct), report, clock.Today);
        if (usable.TryGetValue(Suppliers, out var t)) run.PlanSuppliers(t);
        if (usable.TryGetValue(Stock, out t)) run.PlanItems(t);
        if (usable.TryGetValue(Lots, out t)) run.PlanLots(t);
        if (usable.TryGetValue(Stock, out t)) run.PlanInitialStock(t);
        if (usable.TryGetValue(Orders, out t)) run.PlanOrders(t);

        if (report.HasErrors)
            return report;

        // Dry run writes too and rolls back, so database constraints are checked as well.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.AddRange(run.NewEntities);
            await db.SaveChangesAsync(ct);
            foreach (var (year, last) in run.OrderNumberCounters)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO number_sequence (kind, period, last_value) VALUES ('RN', {year}, {last})
                    ON CONFLICT (kind, period) DO UPDATE SET last_value = GREATEST(number_sequence.last_value, EXCLUDED.last_value)
                    """, ct);
            if (apply)
            {
                await tx.CommitAsync(ct);
                report.Applied = true;
            }
            else
                await tx.RollbackAsync(ct);
        }
        catch (Exception e) when (e is DbUpdateException or DomainException or Npgsql.PostgresException)
        {
            await tx.RollbackAsync(ct);
            report.Error("Upis", null, "Upis u bazu nije uspio, ništa nije upisano: " + e.GetBaseException().Message);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
        return report;
    }

    /// <summary>Finds one CSV per Notion database by name prefix (Notion appends an id; "_all" wins over the view export).</summary>
    public static Dictionary<string, CsvTable> LoadTables(string folder, ImportReport report)
    {
        var tables = new Dictionary<string, CsvTable>();
        if (!Directory.Exists(folder))
        {
            report.Error("Folder", null, $"Folder {folder} ne postoji.");
            return tables;
        }
        var files = Directory.EnumerateFiles(folder, "*.csv", SearchOption.AllDirectories).ToList();
        foreach (var name in RequiredColumns.Keys)
        {
            var matches = files.Where(f => IsExportOf(Path.GetFileName(f), name)).ToList();
            var all = matches.Where(f => f.EndsWith("_all.csv", StringComparison.OrdinalIgnoreCase)).ToList();
            if (all.Count > 0)
                matches = all;
            if (matches.Count == 0)
            {
                report.Warning(name, null, "Fajl nije pronađen; ova baza se preskače.");
                continue;
            }
            if (matches.Count > 1)
            {
                report.Error(name, null, "Više fajlova odgovara ovoj bazi: " + string.Join(", ", matches.Select(Path.GetFileName)) + ". Ostavi jedan.");
                continue;
            }
            var table = NotionCsv.Read(Path.GetFileName(matches[0]), File.ReadAllText(matches[0], Encoding.UTF8));
            tables[name] = table;
            report.Files.Add($"{name}: {Path.GetRelativePath(folder, matches[0])} (redova: {table.Rows.Count})");
        }
        return tables;
    }

    private static bool IsExportOf(string fileName, string database)
    {
        // macOS file names can be decomposed (NFD): "č" as "c" + caron.
        var n = fileName.Normalize(NormalizationForm.FormC);
        return n.StartsWith(database, StringComparison.Ordinal) && n.Length > database.Length && n[database.Length] is ' ' or '_' or '.';
    }

    private static Dictionary<string, T> Map<T>(params (string Name, T Value)[] items) =>
        items.ToDictionary(i => i.Name, i => i.Value, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^RN-(?<year>\d{4})-(?<n>\d{1,9})$")]
    private static partial Regex OrderNumberPattern();

    // ---------------------------------------------------------------------------------------------------------

    private readonly record struct ItemKey(string Name, string? Grade, decimal? ThicknessMm)
    {
        public override string ToString() =>
            string.Join(" ", new[] { Name, Grade, ThicknessMm is { } t ? t.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " mm" : null }.Where(x => x is not null));
    }

    private sealed record LotRef(Guid Id, Guid ItemId, ReceiptDecision Decision, MaterialLot? New);

    /// <summary>Mapped values of one record, labelled with Notion column names (for duplicate/conflict checks).</summary>
    private sealed class Sig(params (string Column, object? Value)[] fields)
    {
        public bool Same(Sig other) => fields.Select(f => f.Value).SequenceEqual(other.Fields.Select(f => f.Value));
        public (string Column, object? Value)[] Fields => fields;
        public string Diff(Sig other) => string.Join(", ", fields.Where((f, i) => !Equals(f.Value, other.Fields[i].Value)).Select(f => f.Column));
    }

    /// <summary>What already is in the database.</summary>
    private sealed class Existing
    {
        public required Dictionary<string, Supplier> Suppliers { get; init; }
        public required Dictionary<ItemKey, MaterialItem> Items { get; init; }
        public required Dictionary<string, MaterialLot> Lots { get; init; }
        public required Dictionary<string, WorkOrder> Orders { get; init; }
        public required Dictionary<Guid, List<string>> OrderLots { get; init; }
        public required Dictionary<string, List<Guid>> EmployeesByName { get; init; }

        public static async Task<Existing> LoadAsync(AppDbContext db, CancellationToken ct)
        {
            var lots = await db.MaterialLots.AsNoTracking().ToListAsync(ct);
            var links = await db.WorkOrderMaterials.AsNoTracking().ToListAsync(ct);
            var lotNumbers = lots.ToDictionary(l => l.Id, l => l.LotNumber);
            return new Existing
            {
                Suppliers = await db.Suppliers.AsNoTracking().ToDictionaryAsync(s => s.Name, StringComparer.Ordinal, ct),
                Items = (await db.MaterialItems.AsNoTracking().ToListAsync(ct)).ToDictionary(i => new ItemKey(i.Name, i.Grade, i.ThicknessMm)),
                Lots = lots.ToDictionary(l => l.LotNumber, StringComparer.Ordinal),
                Orders = await db.WorkOrders.AsNoTracking().ToDictionaryAsync(w => w.Number, StringComparer.Ordinal, ct),
                OrderLots = links.GroupBy(l => l.WorkOrderId).ToDictionary(g => g.Key, g => g.Select(l => lotNumbers[l.MaterialLotId]).Order(StringComparer.Ordinal).ToList()),
                EmployeesByName = (await db.Employees.AsNoTracking().ToListAsync(ct))
                    .GroupBy(e => e.FullName.Trim().Normalize(NormalizationForm.FormC), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList(), StringComparer.OrdinalIgnoreCase),
            };
        }
    }

    /// <summary>Reads one CSV row; every problem goes to the report and marks the row as failed.</summary>
    private sealed class RowReader(Run run, string source, CsvRow row)
    {
        public bool Failed { get; private set; }
        public int Line => row.Line;

        public void Error(string message) { Failed = true; run.Report.Error(source, row.Line, message); }
        public void Warning(string message) => run.Report.Warning(source, row.Line, message);
        public void Duplicate(string message) => run.Report.Duplicate(source, row.Line, message);

        public string Raw(string column) => row.Get(column);

        public string? Text(string column, int max, bool required = false)
        {
            var v = row.Get(column);
            if (v.Length == 0)
            {
                if (required) Error($"{column}: prazno obavezno polje.");
                return null;
            }
            if (v.Length > max)
            {
                Error($"{column}: predugo ({v.Length} znakova, najviše {max}).");
                return null;
            }
            return v;
        }

        public DateOnly? Date(string column, bool required = false)
        {
            var v = row.Get(column);
            if (!NotionCsv.TryDate(v, out var d))
                Error($"{column}: \"{v}\" nije datum u podržanom formatu (2026-10-12, October 12, 2026 ili 12.10.2026).");
            else if (d is null && required)
                Error($"{column}: prazno obavezno polje.");
            return d;
        }

        public decimal? Decimal(string column)
        {
            var v = row.Get(column);
            if (!NotionCsv.TryDecimal(v, out var d))
            {
                Error($"{column}: \"{v}\" nije broj (samo cifre i decimalna tačka, npr. 12.5).");
                return null;
            }
            if (d < 0)
            {
                Error($"{column}: ne može biti negativno ({v}).");
                return null;
            }
            return d;
        }

        public int? Int(string column)
        {
            var v = row.Get(column);
            if (!NotionCsv.TryInt(v, out var n))
                Error($"{column}: \"{v}\" nije cijeli broj.");
            return n;
        }

        public T? Enum<T>(string column, Dictionary<string, T> options) where T : struct
        {
            var v = row.Get(column);
            if (v.Length == 0)
            {
                Error($"{column}: prazno obavezno polje.");
                return null;
            }
            if (options.TryGetValue(v, out var value))
                return value;
            Error($"{column}: nepoznata vrijednost \"{v}\" (dozvoljeno: {string.Join(", ", options.Keys)}).");
            return null;
        }

        /// <summary>Notion person → Employee by exact full name. Unknown: error when required, otherwise warning and empty.</summary>
        public Guid? Person(string column, bool required)
        {
            var v = row.Get(column);
            var names = NotionCsv.Names(v);
            void Problem(string message) { if (required) Error(message); else Warning(message + " Polje ostaje prazno."); }
            if (names.Count == 0)
            {
                if (required) Error($"{column}: prazno obavezno polje.");
                return null;
            }
            if (names.Count > 1)
            {
                Problem($"{column}: više osoba ({v}), a polje prima jednu.");
                return null;
            }
            var name = names[0].Normalize(NormalizationForm.FormC);
            if (!run.Db.EmployeesByName.TryGetValue(name, out var ids))
            {
                Problem($"{column}: zaposlenik \"{name}\" ne postoji u aplikaciji (unesi ga u Podešavanja → Zaposlenici).");
                return null;
            }
            if (ids.Count > 1)
            {
                Problem($"{column}: više zaposlenika se zove \"{name}\".");
                return null;
            }
            return ids[0];
        }

        public void WarnIfFilled(string column, string why)
        {
            if (row.Get(column).Length > 0)
                Warning($"{column}: {why}");
        }

        /// <summary>
        /// Duplicate/conflict check against earlier rows of the same file and the database. True = new record to create.
        /// </summary>
        public bool IsNew<TKey>(string what, TKey key, Sig sig, Dictionary<TKey, (int Line, Sig Sig)> seen, Sig? inDb) where TKey : notnull
        {
            if (seen.TryGetValue(key, out var first))
            {
                if (first.Sig.Same(sig)) Duplicate($"{what} „{key}” je već u redu {first.Line}.");
                else Error($"Konflikt: {what} „{key}” je već u redu {first.Line} sa drugim podacima ({first.Sig.Diff(sig)}).");
                return false;
            }
            seen[key] = (row.Line, sig);
            if (inDb is null)
                return true;
            if (inDb.Same(sig)) Duplicate($"{what} „{key}” već postoji u aplikaciji.");
            else Error($"Konflikt: {what} „{key}” već postoji u aplikaciji sa drugim podacima ({inDb.Diff(sig)}).");
            return false;
        }
    }

    /// <summary>State of one import run: what exists, what is planned, which keys came from rows with errors.</summary>
    private sealed class Run(Existing db, ImportReport report, DateOnly today)
    {
        public Existing Db => db;
        public ImportReport Report => report;
        public List<object> NewEntities { get; } = [];
        public Dictionary<string, int> OrderNumberCounters { get; } = [];

        private readonly Dictionary<string, Guid> _suppliers = new(StringComparer.Ordinal);
        private readonly HashSet<string> _badSuppliers = new(StringComparer.Ordinal);
        private readonly Dictionary<ItemKey, Guid> _items = [];
        private readonly HashSet<ItemKey> _badItems = [];
        private readonly Dictionary<string, HashSet<ItemKey>> _lotItemsFromStock = new(StringComparer.Ordinal);
        private readonly Dictionary<int, ItemKey> _stockRowItem = [];
        private readonly Dictionary<string, LotRef> _lots = new(StringComparer.Ordinal);
        private readonly HashSet<string> _badLots = new(StringComparer.Ordinal);

        private void Add(object entity, string counter)
        {
            NewEntities.Add(entity);
            report.Count(counter);
        }

        public void PlanSuppliers(CsvTable table)
        {
            var seen = new Dictionary<string, (int, Sig)>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                var r = new RowReader(this, Suppliers, row);
                var name = r.Text("Naziv dobavljača", Supplier.NameMax, required: true);
                var materialOrService = r.Text("Materijal / usluga", Supplier.MaterialOrServiceMax);
                var status = r.Enum("Status odobrenja", SupplierStatuses);
                var initial = r.Date("Datum početne ocjene");
                var next = r.Date("Sljedeća ocjena");
                var criteria = r.Text("Kriteriji / rezultat ocjene", Supplier.TextMax);
                var responsible = r.Person("Odgovorna osoba", required: false);
                var notes = r.Text("Napomene", Supplier.TextMax);
                r.WarnIfFilled("Certifikati / dokazi", "fajlovi se ne uvoze; dodaj ih na dobavljaču u aplikaciji.");
                if (name is null)
                    continue;
                if (r.Failed)
                {
                    _badSuppliers.Add(name);
                    continue;
                }

                var sig = SupplierSig(materialOrService, status!.Value, initial, next, criteria, responsible, notes);
                db.Suppliers.TryGetValue(name, out var existing);
                var inDb = existing is null ? null : SupplierSig(existing.MaterialOrService, existing.ApprovalStatus,
                    existing.InitialEvaluationDate, existing.NextEvaluationDate, existing.EvaluationCriteria, existing.ResponsibleEmployeeId, existing.Notes);
                var isNew = r.IsNew("dobavljač", name, sig, seen, inDb);
                if (existing is not null)
                    _suppliers.TryAdd(name, existing.Id);
                if (!isNew)
                    continue;

                var supplier = new Supplier
                {
                    Name = name, MaterialOrService = materialOrService, ApprovalStatus = status.Value,
                    InitialEvaluationDate = initial, NextEvaluationDate = next, EvaluationCriteria = criteria,
                    ResponsibleEmployeeId = responsible, Notes = notes,
                };
                _suppliers[name] = supplier.Id;
                Add(supplier, "Dobavljači");
            }
        }

        private static Sig SupplierSig(string? materialOrService, SupplierApprovalStatus status, DateOnly? initial, DateOnly? next,
            string? criteria, Guid? responsible, string? notes) =>
            new(("Materijal / usluga", materialOrService), ("Status odobrenja", status), ("Datum početne ocjene", initial),
                ("Sljedeća ocjena", next), ("Kriteriji / rezultat ocjene", criteria), ("Odgovorna osoba", responsible), ("Napomene", notes));

        /// <summary>Pass 1 over "Zalihe materijala": the material items (artikal = name + grade + thickness).</summary>
        public void PlanItems(CsvTable table)
        {
            var seen = new Dictionary<ItemKey, (int Line, Sig Sig)>();
            var reportedInDb = new HashSet<ItemKey>();
            foreach (var row in table.Rows)
            {
                var r = new RowReader(this, Stock, row);
                var name = r.Text("Materijal / artikal", MaterialItem.NameMax, required: true);
                var grade = r.Text("Kvalitet / oznaka", MaterialItem.GradeMax);
                var thickness = r.Decimal("Debljina mm");
                var unit = r.Enum("Jedinica", Units);
                var minimum = r.Decimal("Minimalna zaliha");
                var stocktake = r.Date("Datum sljedećeg popisa");
                if (name is null)
                    continue;
                var key = new ItemKey(name, grade, thickness);
                if (r.Failed)
                {
                    _badItems.Add(key);
                    continue;
                }
                _stockRowItem[row.Line] = key;
                foreach (var lot in NotionCsv.Names(r.Raw("Ulazni lot")))
                    (_lotItemsFromStock.TryGetValue(lot, out var set) ? set : _lotItemsFromStock[lot] = []).Add(key);

                // Several stock rows per item (one per lot) are normal: only different item data is a problem.
                var sig = new Sig(("Jedinica", unit), ("Minimalna zaliha", minimum), ("Datum sljedećeg popisa", stocktake));
                if (seen.TryGetValue(key, out var first))
                {
                    if (!first.Sig.Same(sig))
                        r.Error($"Konflikt: artikal „{key}” je već u redu {first.Line} sa drugim podacima ({first.Sig.Diff(sig)}).");
                    continue;
                }
                seen[key] = (row.Line, sig);

                if (db.Items.TryGetValue(key, out var existing))
                {
                    _items[key] = existing.Id;
                    var inDb = new Sig(("Jedinica", existing.Unit), ("Minimalna zaliha", existing.MinimumStock), ("Datum sljedećeg popisa", existing.NextStocktakeDate));
                    if (!inDb.Same(sig))
                        r.Error($"Konflikt: artikal „{key}” već postoji u aplikaciji sa drugim podacima ({inDb.Diff(sig)}).");
                    else if (reportedInDb.Add(key))
                        r.Duplicate($"artikal „{key}” već postoji u aplikaciji.");
                    continue;
                }
                var item = new MaterialItem { Name = name, Grade = grade, ThicknessMm = thickness, Unit = unit!.Value, MinimumStock = minimum, NextStocktakeDate = stocktake };
                _items[key] = item.Id;
                Add(item, "Artikli");
            }
        }

        private Guid? ResolveItem(RowReader r, ItemKey key, string how)
        {
            if (_items.TryGetValue(key, out var id))
                return id;
            if (db.Items.TryGetValue(key, out var existing))
                return existing.Id;
            r.Error(_badItems.Contains(key)
                ? $"Artikal „{key}” ({how}) ima grešku u Zalihama materijala."
                : $"Artikal „{key}” ({how}) ne postoji ni u Zalihama materijala ni u aplikaciji.");
            return null;
        }

        public void PlanLots(CsvTable table)
        {
            var seen = new Dictionary<string, (int, Sig)>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                var r = new RowReader(this, Lots, row);
                var lotNumber = r.Text("Oznaka lota / heat broj", MaterialLot.LotNumberMax, required: true);

                // Item: from the stock row that holds this lot, else from the lot's own text columns.
                Guid? itemId = null;
                var fromStock = lotNumber is null ? null : _lotItemsFromStock.GetValueOrDefault(lotNumber);
                var kind = r.Text("Vrsta materijala", MaterialItem.NameMax);
                var grade = r.Text("Kvalitet / oznaka", MaterialItem.GradeMax);
                var thickness = r.Decimal("Debljina mm");
                if (fromStock is { Count: > 1 })
                    r.Error($"Konflikt: lot je u Zalihama materijala vezan za različite artikle ({string.Join("; ", fromStock)}).");
                else if (fromStock is { Count: 1 })
                {
                    var key = fromStock.First();
                    itemId = ResolveItem(r, key, "iz Zaliha materijala");
                    if (kind is not null && new ItemKey(kind, grade, thickness) != key)
                        r.Warning($"Vrsta materijala / Kvalitet / Debljina na lotu se razlikuju od artikla „{key}” u Zalihama; uzet je artikal iz Zaliha.");
                }
                else if (kind is null)
                    r.Error("Artikal nije poznat: lot nije ni u jednom redu Zaliha materijala, a Vrsta materijala je prazna.");
                else
                    itemId = ResolveItem(r, new ItemKey(kind, grade, thickness), "Vrsta materijala / Kvalitet / Debljina");

                var supplierId = ResolveSupplier(r);
                var receivedDate = r.Date("Datum prijema", required: true);
                if (receivedDate > today)
                    r.Error("Datum prijema: ne može biti u budućnosti.");
                var dimensions = r.Text("Dimenzije / količina", MaterialLot.DimensionsMax);
                var location = r.Text("Skladišna lokacija", MaterialLot.StorageLocationMax);
                var decision = r.Enum("Odluka prijema", Decisions);
                Guid? inspector = null;
                if (decision == ReceiptDecision.NaKontroli)
                    r.WarnIfFilled("Kontrolisao", "ne uvozi se dok je lot na kontroli; upisuje se uz odluku prijema.");
                else if (decision is not null)
                    inspector = r.Person("Kontrolisao", required: true);
                r.WarnIfFilled("Certifikat materijala", "fajl se ne uvozi; dodaj certifikat na lotu u aplikaciji.");
                if (lotNumber is null)
                    continue;
                if (r.Failed)
                {
                    _badLots.Add(lotNumber);
                    continue;
                }

                var sig = LotSig(itemId!.Value, supplierId!.Value, receivedDate!.Value, dimensions, decision!.Value, inspector, location);
                db.Lots.TryGetValue(lotNumber, out var existing);
                if (existing is { IsVoided: true })
                {
                    r.Error($"Lot „{lotNumber}” postoji u aplikaciji kao poništen.");
                    continue;
                }
                var inDb = existing is null ? null : LotSig(existing.MaterialItemId, existing.SupplierId, existing.ReceivedDate,
                    existing.DimensionsDescription, existing.ReceiptDecision, existing.InspectedByEmployeeId, existing.StorageLocation);
                var isNew = r.IsNew("lot", lotNumber, sig, seen, inDb);
                if (existing is not null)
                    _lots.TryAdd(lotNumber, new LotRef(existing.Id, existing.MaterialItemId, existing.ReceiptDecision, null));
                if (!isNew)
                    continue;

                var lot = MaterialLot.ImportedFromNotion(lotNumber, itemId.Value, supplierId.Value, receivedDate.Value, today);
                lot.DimensionsDescription = dimensions;
                lot.StorageLocation = location;
                if (decision != ReceiptDecision.NaKontroli)
                    lot.Decide(decision.Value, inspector, decision == ReceiptDecision.Odbijen ? "Odbijen u Notionu" : null, onHand: 0, today);
                _lots[lotNumber] = new LotRef(lot.Id, lot.MaterialItemId, lot.ReceiptDecision, lot);
                Add(lot, "Lotovi");
            }
        }

        private static Sig LotSig(Guid itemId, Guid supplierId, DateOnly received, string? dimensions, ReceiptDecision decision, Guid? inspector, string? location) =>
            new(("artikal", itemId), ("dobavljač", supplierId), ("Datum prijema", received), ("Dimenzije / količina", dimensions),
                ("Odluka prijema", decision), ("Kontrolisao", inspector), ("Skladišna lokacija", location));

        private Guid? ResolveSupplier(RowReader r)
        {
            var registered = NotionCsv.Names(r.Raw("Dobavljač u registru"));
            var text = r.Raw("Dobavljač");
            if (registered.Count > 1)
            {
                r.Error($"Dobavljač u registru: više dobavljača ({string.Join(", ", registered)}), a lot ima jednog.");
                return null;
            }
            var name = registered.FirstOrDefault();
            if (name is not null && text.Length > 0 && text != name)
            {
                r.Error($"Konflikt: Dobavljač u registru je „{name}”, a tekst Dobavljač je „{text}”.");
                return null;
            }
            name ??= text.Length > 0 ? text : null;
            if (name is null)
            {
                r.Error("Dobavljač u registru: prazno obavezno polje (i tekst Dobavljač je prazan).");
                return null;
            }
            if (_suppliers.TryGetValue(name, out var id))
                return id;
            if (db.Suppliers.TryGetValue(name, out var existing))
                return existing.Id;
            r.Error(_badSuppliers.Contains(name)
                ? $"Dobavljač „{name}” ima grešku u fajlu Dobavljači."
                : $"Dobavljač „{name}” ne postoji ni u fajlu Dobavljači ni u aplikaciji.");
            return null;
        }

        private LotRef? ResolveLot(RowReader r, string lotNumber)
        {
            if (_lots.TryGetValue(lotNumber, out var lot))
                return lot;
            if (db.Lots.TryGetValue(lotNumber, out var existing))
                return new LotRef(existing.Id, existing.MaterialItemId, existing.ReceiptDecision, null);
            r.Error(_badLots.Contains(lotNumber)
                ? $"Lot „{lotNumber}” ima grešku u fajlu Ulazni materijal."
                : $"Lot „{lotNumber}” ne postoji ni u fajlu Ulazni materijal ni u aplikaciji.");
            return null;
        }

        /// <summary>Pass 2 over "Zalihe materijala": Količina na stanju → Korekcija "Početno stanje iz Notiona" on the lot.</summary>
        public void PlanInitialStock(CsvTable table)
        {
            var seen = new Dictionary<string, (int Line, decimal Qty)>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                var r = new RowReader(this, Stock, row);
                var reserved = r.Raw("Rezervisano");
                if (reserved.Length > 0 && !(NotionCsv.TryDecimal(reserved, out var rv) && rv == 0))
                    r.Warning($"Rezervisano ({reserved}) se ne uvozi: rezervacije se unose na nalogu (tab Materijal).");
                var lots = NotionCsv.Names(r.Raw("Ulazni lot"));
                if (r.Raw("Količina na stanju").Length == 0)
                {
                    r.Warning("Količina na stanju je prazna: stanje je nepoznato, početno stanje se ne upisuje.");
                    continue;
                }
                var qty = r.Decimal("Količina na stanju");
                if (qty is null or 0)
                    continue;
                if (lots.Count == 0)
                {
                    r.Error("Količina na stanju bez lota: stanje se vodi po lotu, poveži red sa lotom (Ulazni lot).");
                    continue;
                }
                if (lots.Count > 1)
                {
                    r.Error($"Ulazni lot: više lotova ({string.Join(", ", lots)}) u jednom redu; količina se ne može raspodijeliti.");
                    continue;
                }
                var lotNumber = lots[0];
                var lot = ResolveLot(r, lotNumber);
                if (lot is null)
                    continue;
                if (_stockRowItem.TryGetValue(row.Line, out var rowItem) && _items.TryGetValue(rowItem, out var rowItemId) && rowItemId != lot.ItemId)
                {
                    r.Error($"Konflikt: lot „{lotNumber}” je u aplikaciji vezan za drugi artikal, ne „{rowItem}”.");
                    continue;
                }
                if (seen.TryGetValue(lotNumber, out var first))
                {
                    if (first.Qty == qty) r.Duplicate($"početno stanje lota „{lotNumber}” je već u redu {first.Line}.");
                    else r.Error($"Konflikt: lot „{lotNumber}” već ima stanje {first.Qty} u redu {first.Line}, ovdje {qty}.");
                    continue;
                }
                seen[lotNumber] = (row.Line, qty.Value);
                if (lot.New is null)
                {
                    r.Warning($"Lot „{lotNumber}” već postoji u aplikaciji; početno stanje se ne dodaje (ispravka ide korekcijom na lotu).");
                    continue;
                }
                if (lot.Decision == ReceiptDecision.Odbijen)
                {
                    r.Error($"Lot „{lotNumber}” je odbijen, a ima stanje {qty}: odbijen lot se otpisuje.");
                    continue;
                }
                Add(Domain.Stock.Correction(lot.New, qty.Value, InitialStockReason, today, onHand: 0, today), "Početno stanje (korekcije)");
            }
        }

        public void PlanOrders(CsvTable table)
        {
            var seen = new Dictionary<string, (int, Sig)>(StringComparer.Ordinal);
            foreach (var row in table.Rows)
            {
                var r = new RowReader(this, Orders, row);
                var number = r.Text("Broj naloga", WorkOrder.NumberMax, required: true);
                var customer = r.Text("Kupac", WorkOrder.CustomerMax, required: true);
                var product = r.Text("Proizvod / opis", WorkOrder.ProductDescriptionMax, required: true);
                var qty1 = r.Int("Kolicina Narudzbe");
                var qty2 = r.Int("Naručena količina");
                int? ordered = null;
                if (qty1 is not null && qty2 is not null && qty1 != qty2)
                    r.Error($"Konflikt: Kolicina Narudzbe ({qty1}) i Naručena količina ({qty2}) se razlikuju.");
                else if ((qty1 ?? qty2) is not { } q)
                {
                    if (!r.Failed) r.Error("Kolicina Narudzbe / Naručena količina: prazno obavezno polje.");
                }
                else if (q <= 0)
                    r.Error($"Naručena količina mora biti veća od 0 ({q}).");
                else
                    ordered = q;
                var due = r.Date("Rok isporuke", required: true);
                var release = r.Date("Datum puštanja");
                var status = r.Enum("Status", Statuses);
                var priority = r.Enum("Prioritet", Priorities);
                var approval = r.Enum("Odobrenje kvaliteta", Approvals);
                var drawing = r.Text("Broj crteža", WorkOrder.DrawingNumberMax);
                var revision = r.Text("Revizija crteža", WorkOrder.DrawingRevisionMax);
                var responsible = r.Person("Odgovorna osoba", required: false);

                var related = NotionCsv.Names(r.Raw("Materijalni lotovi"));
                var text = NotionCsv.Names(r.Raw("Lot materijala"));
                if (related.Count > 0 && text.Count > 0 && !related.ToHashSet(StringComparer.Ordinal).SetEquals(text))
                    r.Error($"Konflikt: Materijalni lotovi ({string.Join(", ", related)}) i Lot materijala ({string.Join(", ", text)}) se razlikuju.");
                var lotNumbers = (related.Count > 0 ? related : text).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                var lots = lotNumbers.Select(n => ResolveLot(r, n)).ToList();

                var completed = r.Raw("Odradjeno");
                if (completed.Length > 0 && !(NotionCsv.TryDecimal(completed, out var c) && c == 0))
                    r.Warning($"Odradjeno ({completed}) se ne uvozi: računa se iz dnevnih zapisa proizvodnje.");
                foreach (var box in QualityCheckboxes.Where(b => NotionCsv.IsChecked(r.Raw(b))))
                    r.Warning($"Kvačica „{box}” se ne uvozi: računa se iz kontrola kvaliteta (unesi kontrolu).");
                r.WarnIfFilled("Važeći crtež", "fajl se ne uvozi; dodaj crtež na nalogu u aplikaciji.");
                r.WarnIfFilled("Neusaglašenosti / CAPA", "neusaglašenosti nisu dio importa; unose se u Kvalitet.");
                if (number is null || r.Failed)
                    continue;

                var sig = OrderSig(customer!, product!, ordered!.Value, due!.Value, release, status!.Value, priority!.Value, approval!.Value,
                    drawing, revision, responsible, lotNumbers);
                db.Orders.TryGetValue(number, out var existing);
                var inDb = existing is null ? null : OrderSig(existing.Customer, existing.ProductDescription, existing.OrderedQty, existing.DueDate,
                    existing.ReleaseDate, existing.Status, existing.Priority, existing.QualityApproval, existing.DrawingNumber, existing.DrawingRevision,
                    existing.ResponsibleEmployeeId, db.OrderLots.GetValueOrDefault(existing.Id) ?? []);
                if (!r.IsNew("nalog", number, sig, seen, inDb))
                    continue;

                var order = new WorkOrder
                {
                    Number = number, Customer = customer!, ProductDescription = product!, OrderedQty = ordered.Value, DueDate = due.Value,
                    ReleaseDate = release, Status = status.Value, Priority = priority.Value, QualityApproval = approval.Value,
                    DrawingNumber = drawing, DrawingRevision = revision, ResponsibleEmployeeId = responsible,
                };
                Add(order, "Nalozi");
                foreach (var lot in lots)
                    Add(WorkOrderMaterial.TraceOnly(order.Id, lot!.Id), "Veze lot–nalog (bez rezervacije)");

                var m = OrderNumberPattern().Match(number);
                if (m.Success)
                {
                    var year = m.Groups["year"].Value;
                    OrderNumberCounters[year] = Math.Max(OrderNumberCounters.GetValueOrDefault(year), int.Parse(m.Groups["n"].Value));
                }
            }
        }

        private static Sig OrderSig(string customer, string product, int ordered, DateOnly due, DateOnly? release, WorkOrderStatus status,
            Priority priority, QualityApproval approval, string? drawing, string? revision, Guid? responsible, List<string> lots) =>
            new(("Kupac", customer), ("Proizvod / opis", product), ("Naručena količina", ordered), ("Rok isporuke", due),
                ("Datum puštanja", release), ("Status", status), ("Prioritet", priority), ("Odobrenje kvaliteta", approval),
                ("Broj crteža", drawing), ("Revizija crteža", revision), ("Odgovorna osoba", responsible),
                ("Materijalni lotovi", string.Join(", ", lots)));
    }
}
