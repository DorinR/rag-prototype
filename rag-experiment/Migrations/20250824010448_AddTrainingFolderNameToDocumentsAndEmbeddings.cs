using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingFolderNameToDocumentsAndEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrainingFolderName",
                table: "Embeddings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingFolderName",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainingFolderName",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "TrainingFolderName",
                table: "Documents");
        }
    }
}
