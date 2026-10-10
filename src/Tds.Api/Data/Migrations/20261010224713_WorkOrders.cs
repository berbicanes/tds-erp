using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    product_description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordered_qty = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quality_approval = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    drawing_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    drawing_revision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    drawing_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_employee_responsible_employee_id",
                        column: x => x.responsible_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_file_attachment_drawing_file_id",
                        column: x => x.drawing_file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_drawing_file_id",
                table: "work_order",
                column: "drawing_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_number",
                table: "work_order",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_order_responsible_employee_id",
                table: "work_order",
                column: "responsible_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_status_due_date",
                table: "work_order",
                columns: new[] { "status", "due_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order");
        }
    }
}
