using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineVotingApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraintUserVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserVotes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_UserVotes_AnswerOptionId",
                table: "UserVotes",
                column: "AnswerOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVotes_PollId",
                table: "UserVotes",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVotes_UserId_PollId",
                table: "UserVotes",
                columns: new[] { "UserId", "PollId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserVotes_Answers_AnswerOptionId",
                table: "UserVotes",
                column: "AnswerOptionId",
                principalTable: "Answers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVotes_AspNetUsers_UserId",
                table: "UserVotes",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserVotes_Polls_PollId",
                table: "UserVotes",
                column: "PollId",
                principalTable: "Polls",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserVotes_Answers_AnswerOptionId",
                table: "UserVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVotes_AspNetUsers_UserId",
                table: "UserVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserVotes_Polls_PollId",
                table: "UserVotes");

            migrationBuilder.DropIndex(
                name: "IX_UserVotes_AnswerOptionId",
                table: "UserVotes");

            migrationBuilder.DropIndex(
                name: "IX_UserVotes_PollId",
                table: "UserVotes");

            migrationBuilder.DropIndex(
                name: "IX_UserVotes_UserId_PollId",
                table: "UserVotes");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "UserVotes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
