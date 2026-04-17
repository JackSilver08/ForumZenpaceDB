using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumZenpace.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupInvitationId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GroupInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    ReceiverId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupInvitations_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_GroupInvitationId",
                table: "Notifications",
                column: "GroupInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_GroupId_ReceiverId_Status",
                table: "GroupInvitations",
                columns: new[] { "GroupId", "ReceiverId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_ReceiverId",
                table: "GroupInvitations",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupInvitations_SenderId",
                table: "GroupInvitations",
                column: "SenderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GroupInvitations_GroupInvitationId",
                table: "Notifications",
                column: "GroupInvitationId",
                principalTable: "GroupInvitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GroupInvitations_GroupInvitationId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "GroupInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_GroupInvitationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "GroupInvitationId",
                table: "Notifications");
        }
    }
}
