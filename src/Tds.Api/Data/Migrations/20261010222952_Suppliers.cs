using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Suppliers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "supplier",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    material_or_service = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    approval_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    initial_evaluation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_evaluation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    evaluation_criteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_supplier", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_employee_responsible_employee_id",
                        column: x => x.responsible_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_name",
                table: "supplier",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_responsible_employee_id",
                table: "supplier",
                column: "responsible_employee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier");
        }
    }
}
