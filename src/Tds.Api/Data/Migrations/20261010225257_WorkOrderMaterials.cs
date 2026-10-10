using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order_material",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserved_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_material", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_material_material_lot_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_material_work_order_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_material_lot_id_work_order_id_type",
                table: "stock_movement",
                columns: new[] { "material_lot_id", "work_order_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_work_order_id",
                table: "stock_movement",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_material_material_lot_id",
                table: "work_order_material",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_material_work_order_id_material_lot_id",
                table: "work_order_material",
                columns: new[] { "work_order_id", "material_lot_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movement_work_orders_work_order_id",
                table: "stock_movement",
                column: "work_order_id",
                principalTable: "work_order",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_movement_work_orders_work_order_id",
                table: "stock_movement");

            migrationBuilder.DropTable(
                name: "work_order_material");

            migrationBuilder.DropIndex(
                name: "ix_stock_movement_material_lot_id_work_order_id_type",
                table: "stock_movement");

            migrationBuilder.DropIndex(
                name: "ix_stock_movement_work_order_id",
                table: "stock_movement");
        }
    }
}
