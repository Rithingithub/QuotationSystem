using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotationSystem.Migrations
{
    /// <inheritdoc />
    public partial class coursetypechange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationCourses_Courses_CourseId",
                table: "QuotationCourses");

            migrationBuilder.RenameColumn(
                name: "CourseId",
                table: "QuotationCourses",
                newName: "CourseOptionId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "QuotationCourses",
                newName: "QuotationCourseId");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationCourses_CourseId",
                table: "QuotationCourses",
                newName: "IX_QuotationCourses_CourseOptionId");

            migrationBuilder.AddColumn<string>(
                name: "SelectedDuration",
                table: "QuotationCourses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CourseTypes",
                columns: table => new
                {
                    CourseTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTypes", x => x.CourseTypeId);
                });

            migrationBuilder.CreateTable(
                name: "CourseOptions",
                columns: table => new
                {
                    CourseOptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    CourseTypeId = table.Column<int>(type: "int", nullable: false),
                    FullCoursePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HalfCoursePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOptions", x => x.CourseOptionId);
                    table.ForeignKey(
                        name: "FK_CourseOptions_CourseTypes_CourseTypeId",
                        column: x => x.CourseTypeId,
                        principalTable: "CourseTypes",
                        principalColumn: "CourseTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseOptions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "CourseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOptions_CourseId",
                table: "CourseOptions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOptions_CourseTypeId",
                table: "CourseOptions",
                column: "CourseTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses",
                column: "CourseOptionId",
                principalTable: "CourseOptions",
                principalColumn: "CourseOptionId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationCourses_CourseOptions_CourseOptionId",
                table: "QuotationCourses");

            migrationBuilder.DropTable(
                name: "CourseOptions");

            migrationBuilder.DropTable(
                name: "CourseTypes");

            migrationBuilder.DropColumn(
                name: "SelectedDuration",
                table: "QuotationCourses");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "CourseOptionId",
                table: "QuotationCourses",
                newName: "CourseId");

            migrationBuilder.RenameColumn(
                name: "QuotationCourseId",
                table: "QuotationCourses",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_QuotationCourses_CourseOptionId",
                table: "QuotationCourses",
                newName: "IX_QuotationCourses_CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationCourses_Courses_CourseId",
                table: "QuotationCourses",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "CourseId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
