using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdrPortal.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalAdrLibraryPhase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalAdrs",
                columns: table => new
                {
                    GlobalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdrs", x => x.GlobalId);
                });

            migrationBuilder.CreateTable(
                name: "GlobalAdrInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GlobalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalAdrNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RepoRelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    LastKnownStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseTemplateVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdrInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalAdrInstances_GlobalAdrs_GlobalId",
                        column: x => x.GlobalId,
                        principalTable: "GlobalAdrs",
                        principalColumn: "GlobalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GlobalAdrInstances_ManagedRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "ManagedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalAdrInstances_GlobalId_RepositoryId_LocalAdrNumber",
                table: "GlobalAdrInstances",
                columns: new[] { "GlobalId", "RepositoryId", "LocalAdrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalAdrInstances_RepositoryId",
                table: "GlobalAdrInstances",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalAdrInstances");

            migrationBuilder.DropTable(
                name: "GlobalAdrs");
        }
    }
}
