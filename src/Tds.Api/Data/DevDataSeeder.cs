using Microsoft.EntityFrameworkCore;
using Tds.Api.Domain;
using Tds.Api.Infrastructure;

namespace Tds.Api.Data;

/// <summary>
/// Example data for local development only (CLAUDE.md "Dev seed"). Refuses to run outside the Development
/// environment, whatever the configuration says, and only fills an empty database.
/// Stock goes through domain rules (Receive → Prijem, Issue → Izdavanje); no quantity is written directly.
/// </summary>
public sealed class DevDataSeeder(AppDbContext db, IHostEnvironment env, IClock clock, DocumentNumberService numbers, ILogger<DevDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!env.IsDevelopment())
            throw new InvalidOperationException($"Dev seed may only run in Development (current: {env.EnvironmentName}).");
        if (await db.WorkOrders.AnyAsync(ct) || await db.MaterialLots.AnyAsync(ct))
        {
            await NotionLotExampleAsync(ct);
            return;
        }

        var today = clock.Today;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Employees
        Employee E(string name, string position) => new() { FullName = name, Position = position };
        var marko = E("Marko Marić", "Operater lasera");
        var edin = E("Edin Begić", "Operater prese");
        var amra = E("Amra Hodžić", "Kontrolor kvaliteta");
        var selma = E("Selma Kovačević", "Zavarivač");
        db.Employees.AddRange(marko, edin, amra, selma);

        // Measuring instrument
        var caliper = new Equipment { Code = "MS-001", Name = "Pomično mjerilo 150 mm", Kind = EquipmentKind.MjernoSredstvo, Manufacturer = "Mitutoyo", CalibrationIntervalMonths = 12 };
        var micrometer = new Equipment { Code = "MS-002", Name = "Mikrometar 0–25 mm", Kind = EquipmentKind.MjernoSredstvo, Manufacturer = "Mitutoyo", CalibrationIntervalMonths = 12 };
        db.Equipment.AddRange(caliper, micrometer);
        db.EquipmentCalibrations.Add(EquipmentCalibration.Record(caliper, today.AddMonths(-23), null, "Metrološki laboratorij Sarajevo", CalibrationResult.Uskladjeno, null, null, today));
        db.EquipmentCalibrations.Add(EquipmentCalibration.Record(caliper, today.AddMonths(-11), null, "Metrološki laboratorij Sarajevo", CalibrationResult.Uskladjeno, null, "Certifikat br. 2025/118", today));
        db.EquipmentCalibrations.Add(EquipmentCalibration.Record(micrometer, today.AddMonths(-13), null, "Metrološki laboratorij Sarajevo", CalibrationResult.Uskladjeno, null, null, today));

        // Suppliers
        var salzgitter = new Supplier { Name = "Salzgitter Mannesmann Stahlhandel", MaterialOrService = "Lim S235, inox 1.4301", ApprovalStatus = SupplierApprovalStatus.Odobren, NextEvaluationDate = today.AddDays(20), ResponsibleEmployeeId = amra.Id };
        var metalia = new Supplier { Name = "Metalia d.o.o.", MaterialOrService = "Aluminij AlMg3, pocinčani lim", ApprovalStatus = SupplierApprovalStatus.UslovnoOdobren, NextEvaluationDate = today.AddMonths(4) };
        var messer = new Supplier { Name = "Messer BH Gas", MaterialOrService = "Tehnički plinovi (N₂, O₂)", ApprovalStatus = SupplierApprovalStatus.Odobren };
        db.Suppliers.AddRange(salzgitter, metalia, messer);

        // Items: S235 2–20 mm, 1.4301, AlMg3
        MaterialItem I(string name, string grade, decimal t, Unit unit, decimal? min) => new() { Name = name, Grade = grade, ThicknessMm = t, Unit = unit, MinimumStock = min, NextStocktakeDate = today.AddDays(45) };
        var s235_2 = I("Lim crni", "S235", 2, Unit.tabla, 10);
        var s235_3 = I("Lim crni", "S235", 3, Unit.tabla, 10);
        var s235_5 = I("Lim crni", "S235", 5, Unit.tabla, 6);
        var s235_10 = I("Lim crni", "S235", 10, Unit.tabla, 4);
        var s235_20 = I("Lim crni", "S235", 20, Unit.tabla, null);
        var inox2 = I("Lim inox", "1.4301", 2, Unit.kg, 300);
        var inox3 = I("Lim inox", "1.4301", 3, Unit.kg, 300);
        var alu4 = I("Lim aluminij", "AlMg3", 4, Unit.tabla, 5);
        db.MaterialItems.AddRange(s235_2, s235_3, s235_5, s235_10, s235_20, inox2, inox3, alu4);
        await db.SaveChangesAsync(ct);

        // Lots (Prijem through the domain)
        MaterialLot Lot(string no, MaterialItem item, Supplier s, int daysAgo, decimal qty, string dims, ReceiptDecision decision)
        {
            var (lot, receipt) = MaterialLot.Receive(no, item.Id, s.Id, s.ApprovalStatus, today.AddDays(-daysAgo), qty, today);
            lot.DimensionsDescription = dims;
            lot.StorageLocation = "Regal " + (char)('A' + no.Length % 4) + (daysAgo % 5 + 1);
            db.MaterialLots.Add(lot);
            db.StockMovements.Add(receipt);
            if (decision != ReceiptDecision.NaKontroli)
                lot.Decide(decision, amra.Id, decision == ReceiptDecision.Odbijen ? "Certifikat ne odgovara heat broju" : null, qty, today);
            return lot;
        }
        var lot3 = Lot("H-4471", s235_3, salzgitter, 12, 24, "3000×1500, 24 table", ReceiptDecision.Prihvacen);
        var lot5 = Lot("H-4502", s235_5, salzgitter, 9, 8, "3000×1500, 8 tabli", ReceiptDecision.Prihvacen);
        var lot2 = Lot("H-4388", s235_2, salzgitter, 30, 6, "2500×1250, 6 tabli", ReceiptDecision.Prihvacen);
        var lotInox = Lot("T-77821", inox2, salzgitter, 6, 420.5m, "2000×1000, 21 tabla", ReceiptDecision.Prihvacen);
        Lot("T-77940", inox3, salzgitter, 1, 610, "2000×1000, 20 tabli", ReceiptDecision.NaKontroli);
        Lot("AL-2291", alu4, metalia, 4, 6, "2000×1000, 6 tabli", ReceiptDecision.Karantin);
        Lot("H-4210", s235_10, salzgitter, 40, 3, "3000×1500, 3 table", ReceiptDecision.Prihvacen);
        await db.SaveChangesAsync(ct);

        // Work orders
        async Task<WorkOrder> Wo(string customer, string product, int qty, int dueInDays, Priority p, string drawing, string rev, WorkOrderStatus status)
        {
            var wo = new WorkOrder
            {
                Number = await numbers.NextAsync(DocumentNumberKind.WorkOrder, today, ct),
                Customer = customer, ProductDescription = product, OrderedQty = qty, DueDate = today.AddDays(dueInDays),
                ReleaseDate = today.AddDays(-10), Priority = p, DrawingNumber = drawing, DrawingRevision = rev,
                ResponsibleEmployeeId = amra.Id, Status = status,
            };
            db.WorkOrders.Add(wo);
            return wo;
        }
        var w1 = await Wo("Metalbau AG", "Nosač ugaoni 120×80, S235 3 mm", 200, 9, Priority.Normalan, "MB-1042", "B", WorkOrderStatus.Laser);
        var w2 = await Wo("Stahlbau Keller GmbH", "Konzola za fasadu, S235 5 mm", 80, 2, Priority.Hitno, "SK-311", "A", WorkOrderStatus.Presanje);
        var w3 = await Wo("Alpen Technik AG", "Poklopac kućišta, inox 1.4301 2 mm", 150, -3, Priority.Normalan, "AT-77", "C", WorkOrderStatus.Kontrola);
        var w4 = await Wo("Krajina Metal d.o.o.", "Ploča temelja 300×300, S235 10 mm", 40, 25, Priority.Normalan, "KM-15", "A", WorkOrderStatus.CekaMaterijal);
        await Wo("Metalbau AG", "Držač cijevi, S235 2 mm", 500, 30, Priority.Normalan, "MB-1077", "A", WorkOrderStatus.Zaprimljen);
        await db.SaveChangesAsync(ct);

        db.WorkOrderMaterials.AddRange(WorkOrderMaterial.Link(w1.Id, lot3, 14), WorkOrderMaterial.Link(w2.Id, lot5, 6), WorkOrderMaterial.Link(w3.Id, lotInox, 200));

        // Quality checks (first piece / final)
        async Task Check(WorkOrder wo, InspectionPhase phase, InspectionResult result, string ch, string measured, int daysAgo)
        {
            db.QualityChecks.Add(new QualityCheck
            {
                Number = await numbers.NextAsync(DocumentNumberKind.QualityCheck, today.AddDays(-daysAgo), ct),
                WorkOrderId = wo.Id, Phase = phase, Characteristic = ch, AcceptanceCriterion = "prema crtežu", MeasuredResult = measured,
                SampleSize = 1, Result = result, InspectedByEmployeeId = amra.Id, InspectionDate = today.AddDays(-daysAgo), MeasuringEquipmentId = caliper.Id,
            });
        }
        await Check(w1, InspectionPhase.LaserskoRezanje, InspectionResult.Prihvaceno, "Mjera 120 ±0,5 mm", "120,1 mm", 3);
        await Check(w2, InspectionPhase.LaserskoRezanje, InspectionResult.Prihvaceno, "Rupe Ø12 +0,2", "Ø12,08", 6);
        await Check(w2, InspectionPhase.HidraulicnoPresanje, InspectionResult.Prihvaceno, "Ugao savijanja 90° ±1°", "90,4°", 4);
        await Check(w3, InspectionPhase.LaserskoRezanje, InspectionResult.Prihvaceno, "Kontura bez srha", "OK vizuelno", 8);
        await Check(w3, InspectionPhase.HidraulicnoPresanje, InspectionResult.Prihvaceno, "Visina 45 ±0,3 mm", "45,1 mm", 6);
        await Check(w3, InspectionPhase.ZavrsnaKontrola, InspectionResult.Prihvaceno, "Završna mjerna kontrola", "OK, 5 kom uzorak", 1);

        // Production entries for the last days (Izdavanje through the domain)
        var m1 = await db.Equipment.SingleAsync(e => e.Code == "M-01", ct);
        var m3 = await db.Equipment.SingleAsync(e => e.Code == "M-03", ct);
        var m4 = await db.Equipment.SingleAsync(e => e.Code == "M-04", ct);
        async Task Entry(WorkOrder wo, int daysAgo, Employee op, Equipment eq, int laser, int press, int good, int scrap, decimal hours, MaterialLot? lot, decimal? consumed)
        {
            var date = today.AddDays(-daysAgo);
            var e = new ProductionEntry
            {
                Number = await numbers.NextAsync(DocumentNumberKind.ProductionEntry, date, ct), Date = date, WorkOrderId = wo.Id,
                OperatorEmployeeId = op.Id, MaterialLotId = lot?.Id, LaserCutQty = laser, PressedQty = press, GoodQty = good,
                ScrapQty = scrap, LaborHours = hours, MaterialConsumed = consumed,
            };
            e.Equipment.Add(new ProductionEntryEquipment { ProductionEntryId = e.Id, EquipmentId = eq.Id });
            db.ProductionEntries.Add(e);
            if (lot is not null && consumed is { } c)
                db.StockMovements.Add(ProductionEntryRules.Issue(e, lot.Id, c));
        }
        await Entry(w1, 2, marko, m1, 90, 0, 0, 2, 7.5m, lot3, 4);
        await Entry(w1, 1, marko, m1, 70, 0, 0, 1, 6m, lot3, 3);
        await Entry(w2, 5, marko, m1, 80, 0, 0, 0, 5m, lot5, 3);
        await Entry(w2, 3, edin, m3, 0, 45, 0, 1, 7m, null, null);
        await Entry(w2, 0, edin, m3, 0, 30, 0, 0, 4.5m, null, null);
        await Entry(w3, 7, marko, m1, 155, 0, 0, 3, 8m, lotInox, 180);
        await Entry(w3, 5, edin, m4, 0, 152, 0, 2, 7.5m, null, null);
        await Entry(w3, 0, selma, m4, 0, 0, 120, 0, 6m, null, null);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _ = w4;
        logger.LogInformation("Development example data seeded");
        await NotionLotExampleAsync(ct);
    }

    public const string NotionLotNumber = "N-AL-0312";

    /// <summary>
    /// DOMAIN §2.6 example: a lot as the Notion import leaves it (no received quantity, no movement), on an item with
    /// a minimum, so "—", "Nepotpuno" and the dashboard warning can be seen. Added once, also to an existing dev database.
    /// </summary>
    private async Task NotionLotExampleAsync(CancellationToken ct)
    {
        if (await db.MaterialLots.AnyAsync(l => l.LotNumber == NotionLotNumber, ct))
            return;
        var item = await db.MaterialItems.FirstOrDefaultAsync(i => i.Name == "Lim aluminij" && i.Grade == "AlMg3", ct);
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Name == "Metalia d.o.o.", ct);
        var inspector = await db.Employees.FirstOrDefaultAsync(e => e.FullName == "Amra Hodžić", ct);
        if (item is null || supplier is null || inspector is null)
            return;
        var today = clock.Today;
        var lot = MaterialLot.ImportedFromNotion(NotionLotNumber, item.Id, supplier.Id, today.AddDays(-75), today);
        lot.DimensionsDescription = "3000×1500 (Notion: količina nije upisana)";
        lot.StorageLocation = "Regal C2";
        lot.Decide(ReceiptDecision.Prihvacen, inspector.Id, null, 0, today);
        db.MaterialLots.Add(lot);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Development example: lot {Lot} without known stock (as imported from Notion)", NotionLotNumber);
    }
}
