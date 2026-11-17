using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HelpGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpGaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProjectVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PackSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TopSections = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Coverage = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpGaps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpGap_RecordedAt",
                table: "HelpGaps",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpGaps");
        }
    }
}
