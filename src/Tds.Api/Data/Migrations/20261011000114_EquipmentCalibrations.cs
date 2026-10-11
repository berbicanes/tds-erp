using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentCalibrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_calibration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calibration_date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_calibration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    performed_by = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    certificate_file_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_equipment_calibration", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_calibration_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_calibration_file_attachments_certificate_file_id",
                        column: x => x.certificate_file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_calibration_certificate_file_id1",
                table: "equipment_calibration",
                column: "certificate_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_calibration_equipment_id_calibration_date",
                table: "equipment_calibration",
                columns: new[] { "equipment_id", "calibration_date" });
            // Move existing calibration data from the equipment card into the history (DATA_MODEL §9a).
            // The old columns stay until the owner confirms their DROP (CLAUDE.md).
            // Rows with only a manually entered next date have no calibration date and are not moved;
            // that value stays in equipment.next_calibration_date.
            migrationBuilder.Sql("""
                INSERT INTO equipment_calibration
                    (id, equipment_id, calibration_date, next_calibration_date, performed_by, result,
                     certificate_file_id, note, created_at, created_by, updated_at, updated_by, is_voided)
                SELECT gen_random_uuid(), e.id, e.last_calibration_date, e.next_calibration_date,
                       'Nije navedeno (preneseno iz kartice opreme)', 'Uskladjeno', e.calibration_certificate_file_id,
                       'Preneseno pri uvođenju istorije kalibracija (Faza 3).', now(), NULL, now(), NULL, false
                FROM equipment e
                WHERE e.kind = 'MjernoSredstvo' AND e.last_calibration_date IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_calibration");
        }
    }
}
