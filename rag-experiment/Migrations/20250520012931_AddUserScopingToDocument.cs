using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopingToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear all existing data from Documents and Embeddings
            migrationBuilder.Sql(@"
                DELETE FROM Documents;
                DELETE FROM Embeddings;
            ");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Embeddings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Documents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Documents");
        }
    }
}
