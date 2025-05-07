using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalImageAI.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageAnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AnalysisResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessingStartedTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedTimestamp = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAnalysisJobs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageAnalysisJobs");
        }
    }
}
