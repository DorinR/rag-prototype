using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperimentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExperimentName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingModelName = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingDimension = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkOverlap = table.Column<int>(type: "INTEGER", nullable: false),
                    StopwordRemoval = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stemming = table.Column<bool>(type: "INTEGER", nullable: false),
                    Lemmatization = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueryExpansion = table.Column<bool>(type: "INTEGER", nullable: false),
                    TopK = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePrecision = table.Column<double>(type: "REAL", nullable: false),
                    AverageRecall = table.Column<double>(type: "REAL", nullable: false),
                    AverageF1Score = table.Column<double>(type: "REAL", nullable: false),
                    DetailedResults = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentResults", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperimentResults");
        }
    }
}
