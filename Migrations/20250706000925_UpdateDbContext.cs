using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotationSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseOptions_CourseTypes_CourseTypeId",
                table: "CourseOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Users_UserId",
                table: "Quotations");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOptions_CourseTypes_CourseTypeId",
                table: "CourseOptions",
                column: "CourseTypeId",
                principalTable: "CourseTypes",
                principalColumn: "CourseTypeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses",
                column: "CourseOptionId",
                principalTable: "CourseOptions",
                principalColumn: "CourseOptionId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_UserId",
                table: "Quotations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseOptions_CourseTypes_CourseTypeId",
                table: "CourseOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Users_UserId",
                table: "Quotations");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOptions_CourseTypes_CourseTypeId",
                table: "CourseOptions",
                column: "CourseTypeId",
                principalTable: "CourseTypes",
                principalColumn: "CourseTypeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses",
                column: "CourseOptionId",
                principalTable: "CourseOptions",
                principalColumn: "CourseOptionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Users_UserId",
                table: "Quotations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
