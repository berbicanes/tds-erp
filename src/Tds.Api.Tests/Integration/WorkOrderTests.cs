using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tds.Api.Data;
using Tds.Api.Domain;
using Tds.Api.Features.WorkOrders;
using Tds.Api.Infrastructure;
using Tds.Api.Tests.Support;
using static Tds.Api.Tests.Support.ApiClientExtensions;

namespace Tds.Api.Tests.Integration;

public class WorkOrderTests(PostgresFixture postgres)
{
    private static string Today => new SystemClock(TimeProvider.System).Today.ToString("yyyy-MM-dd");
    private static string Days(int n) => new SystemClock(TimeProvider.System).Today.AddDays(n).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Create_numbers_orders_per_year_with_defaults_and_derived_values()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var year = new SystemClock(TimeProvider.System).Today.Year;

        var a = await TestData.WorkOrder(k);
        var b = await TestData.WorkOrder(k, "Stahlbau Keller GmbH");

        Assert.Equal($"RN-{year}-0001", a.Number);
        Assert.Equal($"RN-{year}-0002", b.Number);
        Assert.Equal(WorkOrderStatus.Zaprimljen, a.Status);
        Assert.Equal(QualityApproval.Ceka, a.QualityApproval);
        Assert.Equal(0, a.CompletedQty);
        Assert.Equal(0, a.ProgressPercent);
        Assert.False(a.ReadyForShipment);
        Assert.False(a.LaserFirstPieceApproved);
    }

    [Fact]
    public async Task List_filters_active_overdue_due_soon_priority_and_search()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var late = await TestData.WorkOrder(admin, "Kasni d.o.o.", dueDate: Days(-2));
        await TestData.WorkOrder(admin, "Uskoro AG", dueDate: Days(3), priority: "Hitno");
        await TestData.WorkOrder(admin, "Kasnije GmbH", dueDate: Days(40));
        var cancelled = await TestData.WorkOrder(admin, "Otkazani", dueDate: Days(-5));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.WorkOrders.Where(w => w.Id == cancelled.Id).ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WorkOrderStatus.Otkazano), Ct);
        }

        async Task<string[]> Customers(string query) =>
            (await (await admin.GetAsync($"/api/work-orders?{query}", Ct)).ReadAsync<PagedResult<WorkOrderDto>>()).Items.Select(x => x.Customer).ToArray();

        Assert.Equal(["Kasni d.o.o.", "Uskoro AG", "Kasnije GmbH"], await Customers("active=true"));
        Assert.Equal(["Kasni d.o.o."], await Customers("overdue=true"));
        Assert.Equal(["Kasni d.o.o.", "Uskoro AG"], await Customers("dueWithinDays=7"));
        Assert.Equal(["Uskoro AG"], await Customers("priority=Hitno"));
        Assert.Equal(["Otkazani"], await Customers("status=Otkazano"));
        Assert.Equal(["Kasnije GmbH"], await Customers("q=gmbh"));
        Assert.Equal(["Kasnije GmbH", "Uskoro AG", "Kasni d.o.o.", "Otkazani"], await Customers("sort=-dueDate"));

        var lateDto = await (await admin.GetAsync($"/api/work-orders/{late.Id}", Ct)).ReadAsync<WorkOrderDto>();
        Assert.True(lateDto.IsOverdue);
        var cancelledDto = await (await admin.GetAsync($"/api/work-orders/{cancelled.Id}", Ct)).ReadAsync<WorkOrderDto>();
        Assert.False(cancelledDto.IsOverdue);
    }

    [Fact]
    public async Task Validation_messages_are_per_field()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var r = await admin.PostAsJsonAsync("/api/work-orders", new { customer = "", productDescription = "", orderedQty = 0, dueDate = Today, priority = "Normalan" }, Ct);
        var p = await r.ProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("Unesi kupca.", p.FieldError("customer"));
        Assert.Equal("Unesi proizvod / opis.", p.FieldError("productDescription"));
        Assert.Equal("Naručena količina mora biti veća od 0.", p.FieldError("orderedQty"));
    }

    [Fact]
    public async Task Cancelled_order_accepts_only_notes()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var wo = await TestData.WorkOrder(admin);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkOrders.Where(w => w.Id == wo.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WorkOrderStatus.Otkazano), Ct);
        var current = await (await admin.GetAsync($"/api/work-orders/{wo.Id}", Ct)).ReadAsync<WorkOrderDto>();

        object Body(int qty, string? notes) => new
        {
            customer = current.Customer, productDescription = current.ProductDescription, orderedQty = qty, dueDate = current.DueDate,
            priority = "Normalan", drawingNumber = current.DrawingNumber, drawingRevision = current.DrawingRevision, notes, version = current.Version,
        };

        var qtyChange = await admin.PutAsJsonAsync($"/api/work-orders/{wo.Id}", Body(200, null), Ct);
        Assert.Equal($"Nalog {wo.Number} ima status Otkazano; može se mijenjati samo napomena.", (await qtyChange.ProblemAsync(HttpStatusCode.Conflict)).Detail());

        var notes = await (await admin.PutAsJsonAsync($"/api/work-orders/{wo.Id}", Body(100, "Kupac otkazao 10/2026"), Ct)).ReadAsync<WorkOrderDto>();
        Assert.Equal("Kupac otkazao 10/2026", notes.Notes);
    }

    [Fact]
    public async Task Quality_approval_is_a_separate_action_for_kontrolor_and_admin()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var k = await factory.ClientAsAsync(Roles.Kontrolor);
        var wo = await TestData.WorkOrder(k);

        var approved = await (await k.PostAsJsonAsync($"/api/work-orders/{wo.Id}/quality-approval", new { qualityApproval = "Blokirano", version = wo.Version }, Ct)).ReadAsync<WorkOrderDto>();
        Assert.Equal(QualityApproval.Blokirano, approved.QualityApproval);

        var op = await factory.ClientAsAsync(Roles.Operater);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await op.PostAsJsonAsync($"/api/work-orders/{wo.Id}/quality-approval", new { qualityApproval = "Prihvaceno", version = approved.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/work-orders", new { customer = "X" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/work-orders", Ct)).StatusCode);
    }

    [Fact]
    public async Task Derived_fields_sent_by_client_are_ignored()
    {
        await using var factory = await TdsApiFactory.CreateAsync(postgres);
        var admin = await factory.AdminAsync();
        var r = await (await admin.PostAsJsonAsync("/api/work-orders", new
        {
            customer = "Metalbau AG", productDescription = "Nosač", orderedQty = 10, dueDate = Days(10), priority = "Normalan",
            completedQty = 10, finalInspectionPassed = true, readyForShipment = true, status = "Zavrseno", qualityApproval = "Prihvaceno",
        }, Ct)).ReadAsync<WorkOrderDto>(HttpStatusCode.Created);

        Assert.Equal(0, r.CompletedQty);
        Assert.False(r.FinalInspectionPassed);
        Assert.False(r.ReadyForShipment);
        Assert.Equal(WorkOrderStatus.Zaprimljen, r.Status);
        Assert.Equal(QualityApproval.Ceka, r.QualityApproval);
    }
}
