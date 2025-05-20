using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    public partial class AddUserScopingToDocumentsAndEmbeddings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear existing data from Documents and Embeddings
            migrationBuilder.Sql(@"
                DELETE FROM Documents;
                DELETE FROM Embeddings;
            ");

            // Add UserId column to Documents table
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1);

            // Add UserId column to Embeddings table
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Embeddings",
                type: "INTEGER",
                nullable: false,
                defaultValue: -1);

            // Add foreign key for Documents
            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Add foreign key for Embeddings
            migrationBuilder.AddForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings");

            // Remove indexes
            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings");

            // Remove UserId columns
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Embeddings");
        }
    }
}