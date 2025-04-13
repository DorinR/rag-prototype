using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class RenameDocumentLinkToDocumentIdAndAddDocumentTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DocumentLink",
                table: "Embeddings",
                newName: "DocumentTitle");

            migrationBuilder.AddColumn<string>(
                name: "DocumentId",
                table: "Embeddings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "Embeddings");

            migrationBuilder.RenameColumn(
                name: "DocumentTitle",
                table: "Embeddings",
                newName: "DocumentLink");
        }
    }
}
