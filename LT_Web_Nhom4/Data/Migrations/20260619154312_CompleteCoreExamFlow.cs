using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LT_Web_Nhom4.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCoreExamFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttemptAnswerSelections",
                columns: table => new
                {
                    AttemptAnswerId = table.Column<int>(type: "int", nullable: false),
                    QuestionOptionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptAnswerSelections", x => new { x.AttemptAnswerId, x.QuestionOptionId });
                    table.ForeignKey(
                        name: "FK_AttemptAnswerSelections_AttemptAnswers_AttemptAnswerId",
                        column: x => x.AttemptAnswerId,
                        principalTable: "AttemptAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptAnswerSelections_QuestionOptions_QuestionOptionId",
                        column: x => x.QuestionOptionId,
                        principalTable: "QuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.DropForeignKey(
                name: "FK_AttemptAnswers_QuestionOptions_SelectedOptionId",
                table: "AttemptAnswers");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_ExamId",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AttemptAnswers_ExamAttemptId_QuestionId",
                table: "AttemptAnswers");

            migrationBuilder.DropIndex(
                name: "IX_AttemptAnswers_SelectedOptionId",
                table: "AttemptAnswers");

            migrationBuilder.Sql(
                """
                INSERT INTO AttemptAnswerSelections (AttemptAnswerId, QuestionOptionId)
                SELECT MIN(Id), SelectedOptionId
                FROM AttemptAnswers
                WHERE SelectedOptionId IS NOT NULL
                GROUP BY ExamAttemptId, QuestionId, SelectedOptionId;

                WITH CanonicalAnswers AS (
                    SELECT ExamAttemptId, QuestionId, MIN(Id) AS KeepId
                    FROM AttemptAnswers
                    GROUP BY ExamAttemptId, QuestionId
                )
                DELETE answer
                FROM AttemptAnswers answer
                INNER JOIN CanonicalAnswers canonical
                    ON canonical.ExamAttemptId = answer.ExamAttemptId
                    AND canonical.QuestionId = answer.QuestionId
                WHERE answer.Id <> canonical.KeepId;
                """);

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Questions",
                newName: "ImagePath");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "Questions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "SelectedOptionId",
                table: "AttemptAnswers");

            migrationBuilder.RenameColumn(
                name: "MaxTabSwitchCount",
                table: "Exams",
                newName: "MaxWarningCount");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Exams",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Exams",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultReleaseMode",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultsReleasedAt",
                table: "Exams",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Exams",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "CoverImagePath",
                table: "Classes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Classes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Classes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntroVideoUrl",
                table: "Classes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Classes",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                """
                UPDATE Exams
                SET Code = 'DE-' + UPPER(SUBSTRING(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 1, 6))
                WHERE Code = '';

                IF EXISTS (
                    SELECT 1
                    FROM ExamAttempts
                    GROUP BY ExamId, UserId
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, N'Không thể áp dụng migration vì có học viên đang có nhiều lượt trong cùng một đề.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_Code",
                table: "Exams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ExamId_UserId",
                table: "ExamAttempts",
                columns: new[] { "ExamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptAnswers_ExamAttemptId_QuestionId",
                table: "AttemptAnswers",
                columns: new[] { "ExamAttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttemptAnswerSelections_QuestionOptionId",
                table: "AttemptAnswerSelections",
                column: "QuestionOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exams_Code",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_ExamAttempts_ExamId_UserId",
                table: "ExamAttempts");

            migrationBuilder.DropIndex(
                name: "IX_AttemptAnswers_ExamAttemptId_QuestionId",
                table: "AttemptAnswers");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ResultReleaseMode",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ResultsReleasedAt",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "CoverImagePath",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "IntroVideoUrl",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Classes");

            migrationBuilder.RenameColumn(
                name: "MaxWarningCount",
                table: "Exams",
                newName: "MaxTabSwitchCount");

            migrationBuilder.AddColumn<int>(
                name: "SelectedOptionId",
                table: "AttemptAnswers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE answer
                SET SelectedOptionId = selected.QuestionOptionId
                FROM AttemptAnswers answer
                OUTER APPLY (
                    SELECT MIN(selection.QuestionOptionId) AS QuestionOptionId
                    FROM AttemptAnswerSelections selection
                    WHERE selection.AttemptAnswerId = answer.Id
                ) selected;
                """);

            migrationBuilder.DropTable(
                name: "AttemptAnswerSelections");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "Questions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ImagePath",
                table: "Questions",
                newName: "ImageUrl");

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempts_ExamId",
                table: "ExamAttempts",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptAnswers_ExamAttemptId_QuestionId",
                table: "AttemptAnswers",
                columns: new[] { "ExamAttemptId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptAnswers_SelectedOptionId",
                table: "AttemptAnswers",
                column: "SelectedOptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptAnswers_QuestionOptions_SelectedOptionId",
                table: "AttemptAnswers",
                column: "SelectedOptionId",
                principalTable: "QuestionOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
