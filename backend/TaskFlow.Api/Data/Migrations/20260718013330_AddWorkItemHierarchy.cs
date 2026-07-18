using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentWorkItemId",
                table: "WorkItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentWorkItemId",
                table: "WorkItems",
                column: "ParentWorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                table: "WorkItems",
                column: "ParentWorkItemId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ParentWorkItemId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ParentWorkItemId",
                table: "WorkItems");
        }
    }
}
