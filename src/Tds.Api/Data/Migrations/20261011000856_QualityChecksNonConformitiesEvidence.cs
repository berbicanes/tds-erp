using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class QualityChecksNonConformitiesEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "calibration_status_at_check",
                table: "quality_check",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "BezMjernogSredstva");

            migrationBuilder.CreateTable(
                name: "non_conformity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    material_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quality_check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    containment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    product_decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    root_cause = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    corrective_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    action_owner_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effectiveness_check_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("pk_non_conformity", x => x.id);
                    table.ForeignKey(
                        name: "fk_non_conformity_employee_action_owner_employee_id",
                        column: x => x.action_owner_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_non_conformity_material_lot_material_lot_id",
                        column: x => x.material_lot_id,
                        principalTable: "material_lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_non_conformity_quality_checks_quality_check_id",
                        column: x => x.quality_check_id,
                        principalTable: "quality_check",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_non_conformity_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_non_conformity_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quality_check_evidence",
                columns: table => new
                {
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality_check_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quality_check_evidence", x => new { x.quality_check_id, x.file_id });
                    table.ForeignKey(
                        name: "fk_quality_check_evidence_file_attachment_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quality_check_evidence_quality_checks_quality_check_id",
                        column: x => x.quality_check_id,
                        principalTable: "quality_check",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_certificate",
                columns: table => new
                {
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_certificate", x => new { x.supplier_id, x.file_id });
                    table.ForeignKey(
                        name: "fk_supplier_certificate_file_attachment_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplier_certificate_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "non_conformity_evidence",
                columns: table => new
                {
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    non_conformity_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_non_conformity_evidence", x => new { x.non_conformity_id, x.file_id });
                    table.ForeignKey(
                        name: "fk_non_conformity_evidence_file_attachment_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_non_conformity_evidence_non_conformities_non_conformity_id",
                        column: x => x.non_conformity_id,
                        principalTable: "non_conformity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_action_owner_employee_id",
                table: "non_conformity",
                column: "action_owner_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_material_lot_id",
                table: "non_conformity",
                column: "material_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_number",
                table: "non_conformity",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_quality_check_id",
                table: "non_conformity",
                column: "quality_check_id",
                unique: true,
                filter: "quality_check_id IS NOT NULL AND NOT is_voided");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_status",
                table: "non_conformity",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_supplier_id",
                table: "non_conformity",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_work_order_id",
                table: "non_conformity",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_non_conformity_evidence_file_id",
                table: "non_conformity_evidence",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_check_evidence_file_id",
                table: "quality_check_evidence",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_certificate_file_id",
                table: "supplier_certificate",
                column: "file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "non_conformity_evidence");

            migrationBuilder.DropTable(
                name: "quality_check_evidence");

            migrationBuilder.DropTable(
                name: "supplier_certificate");

            migrationBuilder.DropTable(
                name: "non_conformity");

            migrationBuilder.DropColumn(
                name: "calibration_status_at_check",
                table: "quality_check");
        }
    }
}
