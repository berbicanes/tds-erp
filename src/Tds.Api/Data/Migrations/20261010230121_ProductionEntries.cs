using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "production_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    laser_cut_qty = table.Column<int>(type: "integer", nullable: false),
                    pressed_qty = table.Column<int>(type: "integer", nullable: false),
                    good_qty = table.Column<int>(type: "integer", nullable: false),
                    scrap_qty = table.Column<int>(type: "integer", nullable: false),
                    labor_hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    material_consumed = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    is_voided = table.Column<bool>(type: "boolean", nullable: false),
                    voided_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    voided_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_production_entry_employee_operator_employee_id",
                        column: x => x.operator_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entry_material_lot_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entry_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_entry_equipment",
                columns: table => new
                {
                    production_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_entry_equipment", x => new { x.production_entry_id, x.equipment_id });
                    table.ForeignKey(
                        name: "fk_production_entry_equipment_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_production_entry_equipment_production_entries_production_en",
                        column: x => x.production_entry_id,
                        principalTable: "production_entry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_production_entry_id",
                table: "stock_movement",
                column: "production_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_date",
                table: "production_entry",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_material_lot_id",
                table: "production_entry",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_number",
                table: "production_entry",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_operator_employee_id",
                table: "production_entry",
                column: "operator_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_work_order_id",
                table: "production_entry",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_entry_equipment_equipment_id",
                table: "production_entry_equipment",
                column: "equipment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movement_production_entry_production_entry_id",
                table: "stock_movement",
                column: "production_entry_id",
                principalTable: "production_entry",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_movement_production_entry_production_entry_id",
                table: "stock_movement");

            migrationBuilder.DropTable(
                name: "production_entry_equipment");

            migrationBuilder.DropTable(
                name: "production_entry");

            migrationBuilder.DropIndex(
                name: "ix_stock_movement_production_entry_id",
                table: "stock_movement");
        }
    }
}
