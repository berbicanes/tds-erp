using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    calibration_interval_months = table.Column<int>(type: "integer", nullable: true),
                    last_calibration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_calibration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    calibration_certificate_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_file_attachments_calibration_certificate_file_id",
                        column: x => x.calibration_certificate_file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_calibration_certificate_file_id",
                table: "equipment",
                column: "calibration_certificate_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_code",
                table: "equipment",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment");
        }
    }
}
