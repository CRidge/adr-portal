using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdrPortal.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialManagedRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManagedRepositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AdrFolder = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false, defaultValue: "docs/adr"),
                    InboxFolder = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    GitRemoteUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedRepositories", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManagedRepositories");
        }
    }
}
