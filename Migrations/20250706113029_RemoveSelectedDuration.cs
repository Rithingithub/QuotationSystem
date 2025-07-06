using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotationSystem.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSelectedDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedDuration",
                table: "QuotationCourses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedDuration",
                table: "QuotationCourses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
