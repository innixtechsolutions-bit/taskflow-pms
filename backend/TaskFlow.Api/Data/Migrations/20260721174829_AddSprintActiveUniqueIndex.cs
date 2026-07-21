using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintActiveUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId_ActiveOnly",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[Status] = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId_ActiveOnly",
                table: "Sprints");
        }
    }
}
