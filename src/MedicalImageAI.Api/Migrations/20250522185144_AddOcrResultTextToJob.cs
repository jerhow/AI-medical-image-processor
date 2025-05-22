using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalImageAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOcrResultTextToJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OcrResultText",
                table: "ImageAnalysisJobs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcrResultText",
                table: "ImageAnalysisJobs");
        }
    }
}
