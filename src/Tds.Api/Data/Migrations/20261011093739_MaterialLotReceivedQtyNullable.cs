using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tds.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaterialLotReceivedQtyNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "received_qty",
                table: "material_lot",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Unknown must not silently become 0 (golden rule 6): refuse while imported lots exist.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM material_lot WHERE received_qty IS NULL) THEN
                        RAISE EXCEPTION 'material_lot has lots with unknown received_qty (imported from Notion)';
                    END IF;
                END $$;
                """);
            migrationBuilder.AlterColumn<decimal>(
                name: "received_qty",
                table: "material_lot",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,3)",
                oldPrecision: 12,
                oldScale: 3,
                oldNullable: true);
        }
    }
}
