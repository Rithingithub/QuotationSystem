using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotationSystem.Migrations
{
    /// <inheritdoc />
    public partial class QuotationCoursePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuotationCoursePrices",
                columns: table => new
                {
                    QuotationCoursePriceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotationCourseId = table.Column<int>(type: "int", nullable: false),
                    FullCoursePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HalfCoursePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsCustomPrice = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationCoursePrices", x => x.QuotationCoursePriceId);
                    table.ForeignKey(
                        name: "FK_QuotationCoursePrices_QuotationCourses_QuotationCourseId",
                        column: x => x.QuotationCourseId,
                        principalTable: "QuotationCourses",
                        principalColumn: "QuotationCourseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationCoursePrices_QuotationCourseId",
                table: "QuotationCoursePrices",
                column: "QuotationCourseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationCoursePrices");
        }
    }
}
