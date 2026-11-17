using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyApp.Infrastructure.Persistence;

#nullable disable

namespace MyApp.Infrastructure.Migrations
{
    /// <summary>The organization logo is stored as a data URL (an image of up to 5 MB), so its column no longer has a length limit.</summary>
    [DbContext(typeof(MyAppDbContext))]
    [Migration("20261106090000_OrganizationLogoData")]
    public partial class OrganizationLogoData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LogoUrl",
                table: "Organizations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
