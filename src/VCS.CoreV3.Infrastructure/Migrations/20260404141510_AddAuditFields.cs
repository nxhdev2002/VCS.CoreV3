using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VCS.CoreV3.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "api_keys",
                newName: "CreatedAtUtc");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "api_keys");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "api_keys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                table: "api_keys",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "api_keys");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "api_keys",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "api_keys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
