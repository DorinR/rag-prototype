using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace rag_experiment.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationBasedArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Disable foreign key constraints (for SQLite)
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;");

            // 2. Drop tables in reverse dependency order
            migrationBuilder.DropTable("Embeddings");    // Child table first
            migrationBuilder.DropTable("Messages");      // Child table  
            migrationBuilder.DropTable("Documents");     // Child table
            migrationBuilder.DropTable("Conversations"); // Parent table
            migrationBuilder.DropTable("Users");         // Root parent

            // 3. Optionally drop migration history to start completely fresh
            // migrationBuilder.DropTable("__EFMigrationsHistory");

            // 4. Re-enable foreign key constraints
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Documents",
                newName: "ConversationId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_UserId",
                table: "Documents",
                newName: "IX_Documents_ConversationId");

            migrationBuilder.AddColumn<int>(
                name: "ConversationId",
                table: "Embeddings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    ConversationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Embeddings_ConversationId",
                table: "Embeddings",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId",
                table: "Conversations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Conversations_ConversationId",
                table: "Documents",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Embeddings_Conversations_ConversationId",
                table: "Embeddings",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Conversations_ConversationId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Embeddings_Conversations_ConversationId",
                table: "Embeddings");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Embeddings_ConversationId",
                table: "Embeddings");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "Embeddings");

            migrationBuilder.RenameColumn(
                name: "ConversationId",
                table: "Documents",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_ConversationId",
                table: "Documents",
                newName: "IX_Documents_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
