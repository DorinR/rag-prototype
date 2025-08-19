using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserIdOptionalInEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Embeddings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Embeddings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Embeddings_Users_UserId",
                table: "Embeddings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
