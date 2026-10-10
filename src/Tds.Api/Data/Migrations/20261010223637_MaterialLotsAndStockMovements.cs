using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaterialLotsAndStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_lot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    material_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    dimensions_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    receipt_decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inspected_by_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_location = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    certificate_file_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_material_lot", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_lot_employee_inspected_by_employee_id",
                        column: x => x.inspected_by_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_material_lot_file_attachment_certificate_file_id",
                        column: x => x.certificate_file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_material_lot_material_item_material_item_id",
                        column: x => x.material_item_id,
                        principalTable: "material_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_material_lot_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    production_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_stock_movement", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_movement_material_lot_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_material_lot_certificate_file_id",
                table: "material_lot",
                column: "certificate_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_lot_inspected_by_employee_id",
                table: "material_lot",
                column: "inspected_by_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_lot_lot_number",
                table: "material_lot",
                column: "lot_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_lot_material_item_id_receipt_decision",
                table: "material_lot",
                columns: new[] { "material_item_id", "receipt_decision" });

            migrationBuilder.CreateIndex(
                name: "ix_material_lot_supplier_id",
                table: "material_lot",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_material_lot_id",
                table: "stock_movement",
                column: "material_lot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movement");

            migrationBuilder.DropTable(
                name: "material_lot");
        }
    }
}
