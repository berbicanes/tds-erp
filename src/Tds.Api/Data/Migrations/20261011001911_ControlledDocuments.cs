using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ControlledDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "controlled_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    version = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    change_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    file_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_controlled_document", x => x.id);
                    table.ForeignKey(
                        name: "fk_controlled_document_employees_owner_employee_id",
                        column: x => x.owner_employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_controlled_document_file_attachments_file_id",
                        column: x => x.file_id,
                        principalTable: "file_attachment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_controlled_document_code_version",
                table: "controlled_document",
                columns: new[] { "code", "version" },
                unique: true,
                filter: "NOT is_voided");

            migrationBuilder.CreateIndex(
                name: "ix_controlled_document_file_id",
                table: "controlled_document",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_controlled_document_owner_employee_id",
                table: "controlled_document",
                column: "owner_employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_controlled_document_one_approved_per_code",
                table: "controlled_document",
                column: "code",
                unique: true,
                filter: "status = 'Odobren' AND NOT is_voided");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "controlled_document");
        }
    }
}
