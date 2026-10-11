using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyEquipmentCalibrationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_equipment_file_attachments_calibration_certificate_file_id",
                table: "equipment");

            migrationBuilder.DropIndex(
                name: "ix_equipment_calibration_certificate_file_id",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "calibration_certificate_file_id",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "last_calibration_date",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "next_calibration_date",
                table: "equipment");

            migrationBuilder.RenameIndex(
                name: "ix_equipment_calibration_certificate_file_id1",
                table: "equipment_calibration",
                newName: "ix_equipment_calibration_certificate_file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_equipment_calibration_certificate_file_id",
                table: "equipment_calibration",
                newName: "ix_equipment_calibration_certificate_file_id1");

            migrationBuilder.AddColumn<Guid>(
                name: "calibration_certificate_file_id",
                table: "equipment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_calibration_date",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "next_calibration_date",
                table: "equipment",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_calibration_certificate_file_id",
                table: "equipment",
                column: "calibration_certificate_file_id");

            migrationBuilder.AddForeignKey(
                name: "fk_equipment_file_attachments_calibration_certificate_file_id",
                table: "equipment",
                column: "calibration_certificate_file_id",
                principalTable: "file_attachment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
