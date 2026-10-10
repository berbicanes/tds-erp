using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quality_check",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    phase = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    characteristic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    acceptance_criterion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    measured_result = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sample_size = table.Column<int>(type: "integer", nullable: true),
                    result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inspected_by_employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_date = table.Column<DateOnly>(type: "date", nullable: false),
                    measuring_equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_quality_check", x => x.id);
                    table.ForeignKey(
                        name: "fk_quality_check_employee_inspected_by_employee_id",
                        column: x => x.inspected_by_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quality_check_equipment_measuring_equipment_id",
                        column: x => x.measuring_equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quality_check_material_lot_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quality_check_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_inspected_by_employee_id",
                table: "quality_check",
                column: "inspected_by_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_material_lot_id",
                table: "quality_check",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_measuring_equipment_id",
                table: "quality_check",
                column: "measuring_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_number",
                table: "quality_check",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_work_order_id_phase_result",
                table: "quality_check",
                columns: new[] { "work_order_id", "phase", "result" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quality_check");
        }
    }
}
