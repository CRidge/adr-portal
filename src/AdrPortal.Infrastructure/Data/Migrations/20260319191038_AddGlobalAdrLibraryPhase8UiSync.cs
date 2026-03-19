using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdrPortal.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalAdrLibraryPhase8UiSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasLocalChanges",
                table: "GlobalAdrInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpdateAvailable",
                table: "GlobalAdrInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GlobalAdrUpdateProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GlobalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalAdrNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedFromVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedTitle = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    ProposedMarkdownContent = table.Column<string>(type: "TEXT", nullable: false),
                    IsPending = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdrUpdateProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalAdrUpdateProposals_GlobalAdrs_GlobalId",
                        column: x => x.GlobalId,
                        principalTable: "GlobalAdrs",
                        principalColumn: "GlobalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GlobalAdrUpdateProposals_ManagedRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "ManagedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlobalAdrVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GlobalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    MarkdownContent = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalAdrVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalAdrVersions_GlobalAdrs_GlobalId",
                        column: x => x.GlobalId,
                        principalTable: "GlobalAdrs",
                        principalColumn: "GlobalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalAdrUpdateProposals_GlobalId_RepositoryId_LocalAdrNumber_IsPending",
                table: "GlobalAdrUpdateProposals",
                columns: new[] { "GlobalId", "RepositoryId", "LocalAdrNumber", "IsPending" });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalAdrUpdateProposals_RepositoryId",
                table: "GlobalAdrUpdateProposals",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalAdrVersions_GlobalId_VersionNumber",
                table: "GlobalAdrVersions",
                columns: new[] { "GlobalId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalAdrUpdateProposals");

            migrationBuilder.DropTable(
                name: "GlobalAdrVersions");

            migrationBuilder.DropColumn(
                name: "HasLocalChanges",
                table: "GlobalAdrInstances");

            migrationBuilder.DropColumn(
                name: "UpdateAvailable",
                table: "GlobalAdrInstances");
        }
    }
}
