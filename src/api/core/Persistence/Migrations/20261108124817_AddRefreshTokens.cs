using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coordina.Api.Core.Persistence.Migrations
{
  /// <inheritdoc />
  public partial class AddRefreshTokens : Migration
  {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(
          name: "auth_refresh_tokens",
          columns: table => new
          {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            user_id = table.Column<Guid>(type: "uuid", nullable: false),
            family_id = table.Column<Guid>(type: "uuid", nullable: false),
            token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true)
          },
          constraints: table =>
          {
            table.PrimaryKey("PK_auth_refresh_tokens", x => x.id);
            table.ForeignKey(
                      name: "FK_auth_refresh_tokens_auth_users_user_id",
                      column: x => x.user_id,
                      principalTable: "auth_users",
                      principalColumn: "id",
                      onDelete: ReferentialAction.Cascade);
          });

      migrationBuilder.CreateIndex(
          name: "IX_auth_refresh_tokens_family_id",
          table: "auth_refresh_tokens",
          column: "family_id");

      migrationBuilder.CreateIndex(
          name: "IX_auth_refresh_tokens_token_hash",
          table: "auth_refresh_tokens",
          column: "token_hash",
          unique: true);

      migrationBuilder.CreateIndex(
          name: "IX_auth_refresh_tokens_user_id",
          table: "auth_refresh_tokens",
          column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropTable(
          name: "auth_refresh_tokens");
    }
  }
}
