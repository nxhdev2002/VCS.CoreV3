using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VCS.CoreV3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateauditfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "api_keys",
                newName: "LastModifierId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "api_keys",
                newName: "LastModificationTime");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "api_keys",
                newName: "CreatorId");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "api_keys",
                newName: "CreationTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastModifierId",
                table: "api_keys",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "LastModificationTime",
                table: "api_keys",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatorId",
                table: "api_keys",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "CreationTime",
                table: "api_keys",
                newName: "CreatedAtUtc");
        }
    }
}
