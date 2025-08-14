using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkIndexAndHashToEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings");

            migrationBuilder.AddColumn<byte[]>(
                name: "ChunkHash",
                table: "Embeddings",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "Embeddings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_UserId_ConversationId_DocumentId_ChunkIndex",
                table: "Embeddings",
                columns: new[] { "UserId", "ConversationId", "DocumentId", "ChunkIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Embeddings_UserId_ConversationId_DocumentId_ChunkIndex",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "ChunkHash",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "Embeddings");

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_UserId",
                table: "Embeddings",
                column: "UserId");
        }
    }
}
