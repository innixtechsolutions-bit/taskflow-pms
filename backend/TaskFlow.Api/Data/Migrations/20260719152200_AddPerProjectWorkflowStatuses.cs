using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding (research.md #4): the tool-generated migration
    // dropped the old Status column and defaulted the new WorkflowStatusId to 0
    // *before* any data existed to back it — that would have destroyed every
    // existing work item's status with no way to recover it. This version instead:
    // creates the table -> adds WorkflowStatusId as nullable -> backfills every
    // project's standard four statuses and every work item's matching new id (while
    // the old Status column still has the data to read) -> tightens the column to
    // NOT NULL -> only then drops the old column. One migration file, one
    // deployment, no dual-write period (FR-007).
    public partial class AddPerProjectWorkflowStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ColorKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStatuses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStatuses_ProjectId_Name",
                table: "WorkflowStatuses",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            // Nullable for now -- tightened to NOT NULL below, once every row is backfilled.
            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatusId",
                table: "WorkItems",
                type: "int",
                nullable: true);

            // Seeds the standard four statuses (FR-005) for every project that already
            // exists -- one INSERT covering every project at once, not a per-project
            // loop (Constitution Principle III).
            migrationBuilder.Sql(@"
                INSERT INTO WorkflowStatuses (ProjectId, Name, Position, Category, ColorKey)
                SELECT Id, 'To Do', 0, 'Open', 'Slate' FROM Projects
                UNION ALL
                SELECT Id, 'In Progress', 1, 'Open', 'Blue' FROM Projects
                UNION ALL
                SELECT Id, 'In Review', 2, 'Open', 'Violet' FROM Projects
                UNION ALL
                SELECT Id, 'Done', 3, 'Done', 'Green' FROM Projects;
            ");

            // Backfills every existing work item's new FK from its old Status value --
            // read here while the old column still exists, dropped further below.
            migrationBuilder.Sql(@"
                UPDATE wi
                SET WorkflowStatusId = ws.Id
                FROM WorkItems wi
                INNER JOIN WorkflowStatuses ws
                    ON ws.ProjectId = wi.ProjectId
                   AND ws.Name = CASE wi.Status
                                     WHEN 'ToDo' THEN 'To Do'
                                     WHEN 'InProgress' THEN 'In Progress'
                                     WHEN 'InReview' THEN 'In Review'
                                     WHEN 'Done' THEN 'Done'
                                 END;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "WorkflowStatusId",
                table: "WorkItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_WorkflowStatusId",
                table: "WorkItems",
                column: "WorkflowStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkflowStatuses_WorkflowStatusId",
                table: "WorkItems",
                column: "WorkflowStatusId",
                principalTable: "WorkflowStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkflowStatuses_WorkflowStatusId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_WorkflowStatusId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkflowStatuses");

            migrationBuilder.DropColumn(
                name: "WorkflowStatusId",
                table: "WorkItems");

            // Data-losing on the way down, like every other column removed by a Down
            // migration in this codebase -- there is no supported path back to a single
            // system-wide fixed status list once a project's statuses are migrated
            // (spec.md Assumptions).
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WorkItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
