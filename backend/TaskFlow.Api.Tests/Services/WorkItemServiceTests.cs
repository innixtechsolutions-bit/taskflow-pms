using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class WorkItemServiceTests : SqlServerTestDatabase
{
    private WorkItemService CreateSut() => new(Db);

    private User AddUser(string email, Role role = Role.Developer)
    {
        var user = new User
        {
            FullName = "Test User",
            Email = email,
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    // Feature 006 — every project now owns its own workflow; test projects get the
    // same standard four statuses ProjectService.CreateAsync seeds in production, so
    // AddWorkItem's default ("To Do") and every EditRequest/filter call below always
    // has a real row to resolve.
    private Project AddProject(string name, int creatorId)
    {
        var project = new Project
        {
            Name = name,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow
        };
        Db.Projects.Add(project);
        Db.SaveChanges();

        Db.WorkflowStatuses.AddRange(
            new WorkflowStatus { ProjectId = project.Id, Name = "To Do", Position = 0, Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Slate },
            new WorkflowStatus { ProjectId = project.Id, Name = "In Progress", Position = 1, Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Blue },
            new WorkflowStatus { ProjectId = project.Id, Name = "In Review", Position = 2, Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Violet },
            new WorkflowStatus { ProjectId = project.Id, Name = "Done", Position = 3, Category = WorkflowStatusCategory.Done, ColorKey = ChipColor.Green });
        Db.SaveChanges();

        return project;
    }

    private int StatusId(int projectId, string statusName) =>
        Db.WorkflowStatuses.Single(s => s.ProjectId == projectId && s.Name == statusName).Id;

    private WorkItem AddWorkItem(
        int projectId, int creatorId, int? assigneeUserId = null,
        string status = "To Do", WorkItemPriority priority = WorkItemPriority.Medium,
        WorkItemType type = WorkItemType.Task, string title = "Some work", DateTime? updatedAt = null)
    {
        var workItem = new WorkItem
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            WorkflowStatusId = StatusId(projectId, status),
            Priority = priority,
            AssigneeUserId = assigneeUserId,
            CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        Db.WorkItems.Add(workItem);
        Db.SaveChanges();
        return workItem;
    }

    private static WorkItemRequest ValidRequest(string title = "Fix the login bug") => new()
    {
        Type = "Task",
        Title = title
    };

    private static WorkItemRequest RequestOfType(string type, int? parentWorkItemId = null, string title = "Some item") => new()
    {
        Type = type,
        Title = title,
        ParentWorkItemId = parentWorkItemId
    };

    [Fact]
    public async Task CreateAsync_rejects_a_parent_on_an_epic()
    {
        var user = AddUser("epic-parent@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherEpic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Other epic");
        var sut = CreateSut();

        await Assert.ThrowsAsync<EpicCannotHaveParentException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Epic", otherEpic.Id)));
    }

    [Fact]
    public async Task CreateAsync_requires_an_epic_parent_for_a_story()
    {
        var user = AddUser("story-noparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ParentRequiredException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Story")));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_non_epic_parent_for_a_story()
    {
        var user = AddUser("story-wrongparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var task = AddWorkItem(project.Id, user.Id, type: WorkItemType.Task, title: "A task");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidParentTypeException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Story", task.Id)));
    }

    [Fact]
    public async Task CreateAsync_creates_a_story_under_an_epic()
    {
        var user = AddUser("story-ok@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var sut = CreateSut();

        var result = await sut.CreateAsync(user.Id, project.Id, RequestOfType("Story", epic.Id));

        Assert.Equal(epic.Id, result.ParentWorkItemId);
    }

    [Fact]
    public async Task CreateAsync_allows_a_task_with_no_parent()
    {
        var user = AddUser("task-noparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var result = await sut.CreateAsync(user.Id, project.Id, RequestOfType("Task"));

        Assert.Null(result.ParentWorkItemId);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_non_story_parent_for_a_task()
    {
        var user = AddUser("task-wrongparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidParentTypeException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Task", epic.Id)));
    }

    [Fact]
    public async Task CreateAsync_requires_a_task_parent_for_a_subtask()
    {
        var user = AddUser("subtask-noparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ParentRequiredException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("SubTask")));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_non_task_parent_for_a_subtask()
    {
        var user = AddUser("subtask-wrongparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var story = AddWorkItem(project.Id, user.Id, type: WorkItemType.Story, title: "Story");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidParentTypeException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("SubTask", story.Id)));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_parent_from_a_different_project()
    {
        var user = AddUser("cross-project@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherProject = AddProject("Beta", user.Id);
        var epicInOtherProject = AddWorkItem(otherProject.Id, user.Id, type: WorkItemType.Epic, title: "Other project epic");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ParentMustBeSameProjectException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Story", epicInOtherProject.Id)));
    }

    [Fact]
    public async Task CreateAsync_rejects_an_unknown_parent_id()
    {
        var user = AddUser("unknown-parent@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<ParentWorkItemNotFoundException>(
            () => sut.CreateAsync(user.Id, project.Id, RequestOfType("Story", 999999)));
    }

    [Fact]
    public async Task GetParentCandidatesAsync_returns_only_same_project_items_of_the_required_parent_type()
    {
        var user = AddUser("candidates@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherProject = AddProject("Beta", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic in project");
        AddWorkItem(project.Id, user.Id, type: WorkItemType.Story, title: "Not a candidate (wrong type)");
        AddWorkItem(otherProject.Id, user.Id, type: WorkItemType.Epic, title: "Epic in other project");
        var sut = CreateSut();

        var candidates = await sut.GetParentCandidatesAsync(project.Id, "Story");

        var candidate = Assert.Single(candidates);
        Assert.Equal(epic.Id, candidate.Id);
    }

    [Fact]
    public async Task GetParentCandidatesAsync_returns_an_empty_list_for_epic()
    {
        var user = AddUser("candidates-epic@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Some epic");
        var sut = CreateSut();

        var candidates = await sut.GetParentCandidatesAsync(project.Id, "Epic");

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task GetParentCandidatesAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetParentCandidatesAsync(999999, "Story"));
    }

    [Fact]
    public async Task GetTreeAsync_nests_a_multi_level_chain_correctly()
    {
        var user = AddUser("tree-nesting@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = new WorkItem
        {
            ProjectId = project.Id, Type = WorkItemType.Story, Title = "Story", CreatedByUserId = user.Id,
            WorkflowStatusId = StatusId(project.Id, "To Do"),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id
        };
        Db.WorkItems.Add(story);
        Db.SaveChanges();
        var task = new WorkItem
        {
            ProjectId = project.Id, Type = WorkItemType.Task, Title = "Task", CreatedByUserId = user.Id,
            WorkflowStatusId = StatusId(project.Id, "To Do"),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = story.Id
        };
        Db.WorkItems.Add(task);
        Db.SaveChanges();
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        var epicNode = Assert.Single(tree);
        Assert.Equal(epic.Id, epicNode.Id);
        var storyNode = Assert.Single(epicNode.Children);
        Assert.Equal(story.Id, storyNode.Id);
        var taskNode = Assert.Single(storyNode.Children);
        Assert.Equal(task.Id, taskNode.Id);
        Assert.Empty(taskNode.Children);
    }

    [Fact]
    public async Task GetTreeAsync_counts_only_direct_children_for_the_done_count()
    {
        var user = AddUser("tree-counts@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var doneId = StatusId(project.Id, "Done");
        var toDoId = StatusId(project.Id, "To Do");
        for (var i = 0; i < 5; i++)
        {
            Db.WorkItems.Add(new WorkItem
            {
                ProjectId = project.Id, Type = WorkItemType.Story, Title = $"Story {i}", CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id,
                WorkflowStatusId = i < 3 ? doneId : toDoId
            });
        }
        Db.SaveChanges();
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        var epicNode = Assert.Single(tree);
        Assert.Equal(5, epicNode.DirectChildrenCount);
        Assert.Equal(3, epicNode.DirectChildrenDoneCount);
    }

    // Feature 006/FR-019 — done-ness is reasoned about via Category, never the literal
    // status name, so a renamed/custom Done-category status still counts as done.
    [Fact]
    public async Task GetTreeAsync_counts_a_renamed_Done_category_status_as_done()
    {
        var user = AddUser("tree-category@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var doneStatus = Db.WorkflowStatuses.Single(s => s.ProjectId == project.Id && s.Name == "Done");
        doneStatus.Name = "Complete";
        Db.SaveChanges();
        Db.WorkItems.Add(new WorkItem
        {
            ProjectId = project.Id, Type = WorkItemType.Story, Title = "Renamed-done child", CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id,
            WorkflowStatusId = doneStatus.Id
        });
        Db.SaveChanges();
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        var epicNode = Assert.Single(tree);
        Assert.Equal(1, epicNode.DirectChildrenDoneCount);
    }

    [Fact]
    public async Task GetTreeAsync_lists_standalone_items_as_top_level_nodes_with_no_children()
    {
        var user = AddUser("tree-standalone@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, type: WorkItemType.Task, title: "Standalone task");
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        var node = Assert.Single(tree);
        Assert.Equal("Standalone task", node.Title);
        Assert.Empty(node.Children);
        Assert.Equal(0, node.DirectChildrenCount);
    }

    [Fact]
    public async Task GetTreeAsync_orders_items_within_a_level_by_most_recently_updated_first()
    {
        var user = AddUser("tree-order@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Oldest", updatedAt: DateTime.UtcNow.AddMinutes(-2));
        AddWorkItem(project.Id, user.Id, title: "Newest", updatedAt: DateTime.UtcNow);
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        Assert.Equal("Newest", tree[0].Title);
        Assert.Equal("Oldest", tree[1].Title);
    }

    [Fact]
    public async Task GetTreeAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetTreeAsync(999999));
    }

    // Feature 006 — columns come from the calling project's own ordered WorkflowStatus
    // list, not a fixed system-wide enum; two projects with different statuses return
    // different column sets (spec.md US1/FR-006/FR-017).
    [Fact]
    public async Task GetBoardAsync_returns_the_projects_own_columns_in_position_order()
    {
        var user = AddUser("board-columns@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id);

        Assert.Equal(
            new[] { ("To Do", "Open"), ("In Progress", "Open"), ("In Review", "Open"), ("Done", "Done") },
            board.Columns.Select(c => (c.Name, c.Category)).ToArray());
    }

    [Fact]
    public async Task GetBoardAsync_returns_different_column_sets_for_different_projects()
    {
        var user = AddUser("board-independence@example.com");
        var projectA = AddProject("Alpha", user.Id);
        var projectB = AddProject("Beta", user.Id);
        Db.WorkflowStatuses.Add(new WorkflowStatus { ProjectId = projectB.Id, Name = "QA", Position = 2, Category = WorkflowStatusCategory.Open, ColorKey = ChipColor.Amber });
        Db.SaveChanges();
        var sut = CreateSut();

        var boardA = await sut.GetBoardAsync(projectA.Id);
        var boardB = await sut.GetBoardAsync(projectB.Id);

        Assert.DoesNotContain(boardA.Columns, c => c.Name == "QA");
        Assert.Contains(boardB.Columns, c => c.Name == "QA");
    }

    [Fact]
    public async Task GetBoardAsync_computes_direct_children_done_counts_for_every_item_not_just_roots()
    {
        var user = AddUser("board-counts@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story", "In Progress");
        AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task A", "Done");
        AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task B", "To Do");
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id);

        // Epic has one direct child (the Story), not done.
        var epicCard = board.Items.Single(i => i.Id == epic.Id);
        Assert.Equal(1, epicCard.DirectChildrenCount);
        Assert.Equal(0, epicCard.DirectChildrenDoneCount);
        // Story has two direct children (the Tasks), one done -- this is the
        // non-root item the flat/paginated endpoint's WorkItemDto has no way to
        // express at all (research.md #2).
        var storyCard = board.Items.Single(i => i.Id == story.Id);
        Assert.Equal(2, storyCard.DirectChildrenCount);
        Assert.Equal(1, storyCard.DirectChildrenDoneCount);
    }

    [Fact]
    public async Task GetBoardAsync_orders_items_by_most_recently_updated_first()
    {
        var user = AddUser("board-order@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Oldest", updatedAt: DateTime.UtcNow.AddMinutes(-2));
        AddWorkItem(project.Id, user.Id, title: "Newest", updatedAt: DateTime.UtcNow);
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id);

        Assert.Equal("Newest", board.Items[0].Title);
        Assert.Equal("Oldest", board.Items[1].Title);
    }

    [Fact]
    public async Task GetBoardAsync_includes_every_item_regardless_of_type_or_depth()
    {
        var user = AddUser("board-alltypes@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        AddChildWorkItem(project.Id, task.Id, user.Id, WorkItemType.SubTask, "SubTask");
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id);

        Assert.Equal(4, board.Items.Count);
    }

    [Fact]
    public async Task GetBoardAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetBoardAsync(999999));
    }

    private WorkItem AddChildWorkItem(int projectId, int parentId, int creatorId, WorkItemType type, string title, string status = "To Do", int? assigneeUserId = null)
    {
        var workItem = new WorkItem
        {
            ProjectId = projectId, Type = type, Title = title, CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = parentId,
            WorkflowStatusId = StatusId(projectId, status), AssigneeUserId = assigneeUserId
        };
        Db.WorkItems.Add(workItem);
        Db.SaveChanges();
        return workItem;
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_parent_fields_when_the_item_has_no_parent()
    {
        var user = AddUser("detail-noparent@example.com");
        var project = AddProject("Alpha", user.Id);
        var item = AddWorkItem(project.Id, user.Id, title: "Standalone");
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(item.Id);

        Assert.Null(result.ParentWorkItemId);
        Assert.Null(result.ParentTitle);
    }

    [Fact]
    public async Task GetByIdAsync_populates_parent_fields_when_a_parent_exists()
    {
        var user = AddUser("detail-parent@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic parent");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story child");
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(story.Id);

        Assert.Equal(epic.Id, result.ParentWorkItemId);
        Assert.Equal("Epic parent", result.ParentTitle);
    }

    [Fact]
    public async Task GetByIdAsync_lists_only_direct_children()
    {
        var user = AddUser("detail-children@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Grandchild task");
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(epic.Id);

        var child = Assert.Single(result.Children);
        Assert.Equal(story.Id, child.Id);
        Assert.Equal("Story", child.Title);
    }

    [Fact]
    public async Task GetByIdAsync_totalDescendantCount_sums_every_level()
    {
        var user = AddUser("detail-descendants@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        AddChildWorkItem(project.Id, task.Id, user.Id, WorkItemType.SubTask, "SubTask");
        var sut = CreateSut();

        var result = await sut.GetByIdAsync(epic.Id);

        Assert.Equal(3, result.TotalDescendantCount);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_entire_subtree_in_one_call()
    {
        var user = AddUser("delete-cascade@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var siblingEpic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Untouched sibling");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        AddChildWorkItem(project.Id, task.Id, user.Id, WorkItemType.SubTask, "SubTask");
        var sut = CreateSut();

        await sut.DeleteAsync(user.Id, "Developer", epic.Id);

        Assert.False(Db.WorkItems.Any(w => w.Id == epic.Id));
        Assert.False(Db.WorkItems.Any(w => w.Id == story.Id));
        Assert.False(Db.WorkItems.Any(w => w.Id == task.Id));
        Assert.True(Db.WorkItems.Any(w => w.Id == siblingEpic.Id));
    }

    [Fact]
    public async Task UpdateAsync_reparents_a_task_to_a_different_story_in_the_same_project()
    {
        var user = AddUser("reparent-ok@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var oldStory = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Old story");
        var newStory = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "New story");
        var task = AddChildWorkItem(project.Id, oldStory.Id, user.Id, WorkItemType.Task, "Task");
        var sut = CreateSut();

        var result = await sut.UpdateAsync(user.Id, "Developer", task.Id, RequestOfType("Task", newStory.Id, "Task"));

        Assert.Equal(newStory.Id, result.ParentWorkItemId);
    }

    [Fact]
    public async Task UpdateAsync_rejects_reparenting_a_task_to_an_epic()
    {
        var user = AddUser("reparent-badtype@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidParentTypeException>(
            () => sut.UpdateAsync(user.Id, "Developer", task.Id, RequestOfType("Task", epic.Id, "Task")));
    }

    [Fact]
    public async Task UpdateAsync_allows_clearing_an_optional_task_parent()
    {
        var user = AddUser("reparent-clear@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        var sut = CreateSut();

        var result = await sut.UpdateAsync(user.Id, "Developer", task.Id, RequestOfType("Task", null, "Task"));

        Assert.Null(result.ParentWorkItemId);
    }

    [Fact]
    public async Task UpdateAsync_rejects_setting_an_item_as_its_own_parent()
    {
        var user = AddUser("reparent-self@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task");
        var sut = CreateSut();

        // A self-reference is caught by the ordinary type check, not a special-cased
        // cycle algorithm — the Task's own type never equals its required parent
        // type, Story (research.md §2).
        await Assert.ThrowsAsync<InvalidParentTypeException>(
            () => sut.UpdateAsync(user.Id, "Developer", task.Id, RequestOfType("Task", task.Id, "Task")));
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_type_change_that_invalidates_the_existing_parent()
    {
        var user = AddUser("typechange-parent@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story");
        var taskUnderStory = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task under story");
        var sut = CreateSut();

        // Changing this Task's type to SubTask would require its existing parent
        // (a Story) to instead be a Task — invalid.
        await Assert.ThrowsAsync<TypeChangeInvalidatesParentException>(
            () => sut.UpdateAsync(user.Id, "Developer", taskUnderStory.Id, RequestOfType("SubTask", story.Id, "Task under story")));
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_type_change_that_invalidates_existing_children()
    {
        var user = AddUser("typechange-children@example.com");
        var project = AddProject("Alpha", user.Id);
        // Standalone (no parent) so only the children-side check is exercised here —
        // a Task with a Story parent would also trip the parent-side check the moment
        // its type changes away from Task, since Story then stops being Task's
        // required parent type, confounding which check actually fired.
        var task = AddWorkItem(project.Id, user.Id, type: WorkItemType.Task, title: "Task");
        AddChildWorkItem(project.Id, task.Id, user.Id, WorkItemType.SubTask, "SubTask child");
        var sut = CreateSut();

        // The Task has a SubTask child, which requires a Task parent — changing the
        // Task itself to Story would leave that SubTask's parent invalid.
        await Assert.ThrowsAsync<TypeChangeInvalidatesChildrenException>(
            () => sut.UpdateAsync(user.Id, "Developer", task.Id, RequestOfType("Story", null, "Task")));
    }

    [Fact]
    public async Task UpdateAsync_allows_a_type_change_with_no_conflicting_parent_or_children()
    {
        var user = AddUser("typechange-ok@example.com");
        var project = AddProject("Alpha", user.Id);
        var story = AddWorkItem(project.Id, user.Id, type: WorkItemType.Story, title: "Story");
        var sut = CreateSut();

        var result = await sut.UpdateAsync(user.Id, "Developer", story.Id, RequestOfType("Task", null, "Story"));

        Assert.Equal("Task", result.Type);
    }

    [Fact]
    public async Task DeleteAsync_authorization_check_applies_only_to_the_item_being_deleted()
    {
        var creator = AddUser("delete-cascade-auth@example.com");
        var stranger = AddUser("delete-cascade-stranger@example.com");
        var project = AddProject("Alpha", creator.Id);
        // The child is created by a different user than the parent, but only the
        // parent's own creator/role is checked (FR-022) — the child's own creator is
        // irrelevant to whether the cascade is allowed.
        var epic = AddWorkItem(project.Id, creator.Id, type: WorkItemType.Epic, title: "Epic");
        var story = new WorkItem
        {
            ProjectId = project.Id, Type = WorkItemType.Story, Title = "Story", CreatedByUserId = stranger.Id,
            WorkflowStatusId = StatusId(project.Id, "To Do"),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id
        };
        Db.WorkItems.Add(story);
        Db.SaveChanges();
        var sut = CreateSut();

        await sut.DeleteAsync(creator.Id, "Developer", epic.Id);

        Assert.False(Db.WorkItems.Any(w => w.Id == story.Id));
    }

    [Fact]
    public async Task CreateAsync_applies_default_priority_and_status_when_omitted()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var result = await sut.CreateAsync(user.Id, project.Id, ValidRequest());

        Assert.Equal("Medium", result.Priority);
        Assert.Equal("To Do", result.StatusName);
        Assert.Equal("Open", result.StatusCategory);
    }

    [Fact]
    public async Task CreateAsync_records_creator_project_and_timestamps()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var result = await sut.CreateAsync(user.Id, project.Id, ValidRequest());

        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal(user.Id, result.CreatedByUserId);
        Assert.Equal("Test User", result.CreatedByName);
        Assert.True(result.CreatedAt >= before);
        Assert.Equal(result.CreatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task CreateAsync_accepts_a_due_date_in_the_past()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.DueDate = DateTime.UtcNow.AddYears(-1);

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(request.DueDate, result.DueDate);
    }

    // Feature 007 (US3) -- start <= due is enforced only when both dates are set;
    // a due date with no start date (covered by the test above) and a start date
    // with no due date are both unconstrained.
    [Theory]
    [InlineData(-1, true)]  // one day before due -> ok
    [InlineData(0, true)]   // exactly on due -> ok
    [InlineData(1, false)]  // one day after due -> rejected
    public async Task CreateAsync_enforces_start_on_or_before_due_when_both_are_set(int startOffsetDaysFromDue, bool expectedValid)
    {
        var user = AddUser("dates@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var due = new DateTime(2026, 8, 1);
        var request = ValidRequest();
        request.DueDate = due;
        request.StartDate = due.AddDays(startOffsetDaysFromDue);

        if (expectedValid)
        {
            var result = await sut.CreateAsync(user.Id, project.Id, request);
            Assert.Equal(request.StartDate, result.StartDate);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDateRangeException>(() => sut.CreateAsync(user.Id, project.Id, request));
        }
    }

    [Fact]
    public async Task CreateAsync_accepts_a_start_date_with_no_due_date()
    {
        var user = AddUser("start-only@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.StartDate = new DateTime(2026, 8, 1);

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(request.StartDate, result.StartDate);
    }

    // Feature 007 (US5) -- label-normalization: trim, 1-30 chars, cap at 5,
    // case-insensitive dedupe within one request, and reuse an existing
    // project label on a case-insensitive name match instead of duplicating it.
    [Fact]
    public async Task CreateAsync_trims_and_persists_labels()
    {
        var user = AddUser("labels-trim@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Labels = ["  backend  ", "urgent"];

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(["backend", "urgent"], result.Labels.OrderBy(l => l).ToList());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_rejects_a_blank_label(string label)
    {
        var user = AddUser($"labels-blank-{Guid.NewGuid():N}@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Labels = [label];

        await Assert.ThrowsAsync<InvalidLabelException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_label_over_30_characters()
    {
        var user = AddUser("labels-toolong@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Labels = [new string('a', 31)];

        await Assert.ThrowsAsync<InvalidLabelException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_sixth_label()
    {
        var user = AddUser("labels-toomany@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Labels = ["a", "b", "c", "d", "e", "f"];

        await Assert.ThrowsAsync<TooManyLabelsException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    [Fact]
    public async Task CreateAsync_dedupes_labels_case_insensitively_within_one_request()
    {
        var user = AddUser("labels-dedupe@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Labels = ["Backend", "backend", "BACKEND"];

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(["Backend"], result.Labels);
    }

    [Fact]
    public async Task CreateAsync_reuses_an_existing_project_label_on_a_case_insensitive_match()
    {
        var user = AddUser("labels-reuse@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var first = ValidRequest("First item");
        first.Labels = ["backend"];
        await sut.CreateAsync(user.Id, project.Id, first);

        var second = ValidRequest("Second item");
        second.Labels = ["BACKEND"];
        await sut.CreateAsync(user.Id, project.Id, second);

        Assert.Equal(1, Db.Labels.Count(l => l.ProjectId == project.Id));
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_whole_label_set()
    {
        var creator = AddUser("labels-update@example.com");
        var project = AddProject("Alpha", creator.Id);
        var sut = CreateSut();
        var createRequest = ValidRequest();
        createRequest.Labels = ["backend", "urgent"];
        var created = await sut.CreateAsync(creator.Id, project.Id, createRequest);

        var editRequest = EditRequest();
        editRequest.Labels = ["frontend"];
        var result = await sut.UpdateAsync(creator.Id, "Developer", created.Id, editRequest);

        Assert.Equal(["frontend"], result.Labels);
    }

    [Fact]
    public async Task UpdateAsync_clears_labels_when_omitted()
    {
        var creator = AddUser("labels-clear@example.com");
        var project = AddProject("Alpha", creator.Id);
        var sut = CreateSut();
        var createRequest = ValidRequest();
        createRequest.Labels = ["backend"];
        var created = await sut.CreateAsync(creator.Id, project.Id, createRequest);

        var result = await sut.UpdateAsync(creator.Id, "Developer", created.Id, EditRequest());

        Assert.Empty(result.Labels);
    }

    [Fact]
    public async Task GetProjectLabelsAsync_returns_only_labels_referenced_by_at_least_one_work_item_alphabetically()
    {
        var user = AddUser("labels-list@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var withLabels = ValidRequest("Item one");
        withLabels.Labels = ["zeta", "alpha"];
        await sut.CreateAsync(user.Id, project.Id, withLabels);
        // An orphan label with no work item attached -- must not appear (research.md #5).
        Db.Labels.Add(new Label { ProjectId = project.Id, Name = "orphan", CreatedAt = DateTime.UtcNow });
        Db.SaveChanges();

        var result = await sut.GetProjectLabelsAsync(project.Id);

        Assert.Equal(["alpha", "zeta"], result);
    }

    [Fact]
    public async Task GetProjectLabelsAsync_returns_an_empty_list_not_an_error_when_the_project_has_no_labels()
    {
        var user = AddUser("labels-empty@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var result = await sut.GetProjectLabelsAsync(project.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProjectLabelsAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetProjectLabelsAsync(999999));
    }

    [Fact]
    public async Task GetWorkItemsAsync_label_filter_matches_case_insensitively_and_combines_with_other_filters()
    {
        var user = AddUser("labels-filter@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        var backendHigh = RequestOfType("Task", title: "Backend high");
        backendHigh.Priority = "High";
        backendHigh.Labels = ["Backend"];
        await sut.CreateAsync(user.Id, project.Id, backendHigh);

        var backendLow = RequestOfType("Task", title: "Backend low");
        backendLow.Priority = "Low";
        backendLow.Labels = ["backend"];
        await sut.CreateAsync(user.Id, project.Id, backendLow);

        var frontend = RequestOfType("Task", title: "Frontend item");
        frontend.Labels = ["frontend"];
        await sut.CreateAsync(user.Id, project.Id, frontend);

        var byLabel = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, null, null, label: "BACKEND");
        var combined = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, "High", null, null, label: "backend");

        Assert.Equal(["Backend high", "Backend low"], byLabel.Items.Select(i => i.Title).OrderBy(t => t).ToList());
        Assert.Equal(["Backend high"], combined.Items.Select(i => i.Title).ToList());
    }

    [Fact]
    public async Task CreateAsync_rejects_an_assignee_that_is_not_an_existing_user()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.AssigneeUserId = 999999;

        await Assert.ThrowsAsync<AssigneeNotFoundException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    [Fact]
    public async Task CreateAsync_throws_for_an_unknown_project()
    {
        var user = AddUser("creator@example.com");
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.CreateAsync(user.Id, 999999, ValidRequest()));
    }

    [Fact]
    public async Task CreateAsync_rejects_an_invalid_type()
    {
        var user = AddUser("creator@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.Type = "NotAType";

        await Assert.ThrowsAsync<InvalidWorkItemTypeException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    // Feature 006 — a StatusId belonging to a *different* project must be rejected;
    // status is identity-based, and identities aren't shared across projects
    // (research.md #7/FR-018).
    [Fact]
    public async Task CreateAsync_rejects_a_statusId_belonging_to_a_different_project()
    {
        var user = AddUser("cross-project-status@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherProject = AddProject("Beta", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.StatusId = StatusId(otherProject.Id, "To Do");

        await Assert.ThrowsAsync<InvalidWorkItemStatusException>(() => sut.CreateAsync(user.Id, project.Id, request));
    }

    private static WorkItemRequest EditRequest(int? statusId = null, string title = "Updated title") => new()
    {
        Type = "Task",
        Title = title,
        StatusId = statusId
    };

    [Theory]
    [InlineData(true, false, "Developer")] // creator
    [InlineData(false, true, "Developer")] // current assignee
    [InlineData(false, false, "Manager")]
    [InlineData(false, false, "Admin")]
    public async Task UpdateAsync_allows_creator_assignee_or_manager_or_admin(bool asCreator, bool asAssignee, string callerRole)
    {
        var creator = AddUser("creator@example.com");
        var assignee = AddUser("assignee@example.com");
        var caller = asCreator ? creator : asAssignee ? assignee : AddUser($"caller-{callerRole}@example.com", Enum.Parse<Role>(callerRole));
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id, assigneeUserId: assignee.Id, updatedAt: DateTime.UtcNow.AddDays(-1));
        var sut = CreateSut();

        var result = await sut.UpdateAsync(caller.Id, callerRole, item.Id, EditRequest(StatusId(project.Id, "Done")));

        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Done", result.StatusName);
        Assert.True(result.UpdatedAt > item.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_caller_who_is_neither_creator_assignee_nor_manager_or_admin()
    {
        var creator = AddUser("creator2@example.com");
        var stranger = AddUser("stranger@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToEditWorkItemException>(
            () => sut.UpdateAsync(stranger.Id, "Developer", item.Id, EditRequest()));
    }

    [Fact]
    public async Task UpdateAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(
            () => sut.UpdateAsync(1, "Admin", 999999, EditRequest()));
    }

    [Fact]
    public async Task UpdateAsync_rejects_a_start_date_after_the_due_date()
    {
        var creator = AddUser("update-dates-bad@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();
        var request = EditRequest();
        request.DueDate = new DateTime(2026, 8, 1);
        request.StartDate = new DateTime(2026, 8, 2);

        await Assert.ThrowsAsync<InvalidDateRangeException>(
            () => sut.UpdateAsync(creator.Id, "Developer", item.Id, request));
    }

    [Fact]
    public async Task UpdateAsync_persists_a_valid_start_date()
    {
        var creator = AddUser("update-dates-ok@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();
        var request = EditRequest();
        request.DueDate = new DateTime(2026, 8, 2);
        request.StartDate = new DateTime(2026, 8, 1);

        var result = await sut.UpdateAsync(creator.Id, "Developer", item.Id, request);

        Assert.Equal(request.StartDate, result.StartDate);
    }

    // Feature 005 (Kanban Board) -- a field-scoped status update, introduced so
    // the board's drag interaction never has to submit (and risk clobbering)
    // fields it doesn't carry, like Description/ParentWorkItemId (research.md #3).
    [Theory]
    [InlineData(true, false, "Developer")] // creator
    [InlineData(false, true, "Developer")] // current assignee
    [InlineData(false, false, "Manager")]
    [InlineData(false, false, "Admin")]
    public async Task UpdateStatusAsync_allows_creator_assignee_or_manager_or_admin(bool asCreator, bool asAssignee, string callerRole)
    {
        var creator = AddUser("status-creator@example.com");
        var assignee = AddUser("status-assignee@example.com");
        var caller = asCreator ? creator : asAssignee ? assignee : AddUser($"status-caller-{callerRole}@example.com", Enum.Parse<Role>(callerRole));
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id, assigneeUserId: assignee.Id, status: "To Do");
        var sut = CreateSut();

        var result = await sut.UpdateStatusAsync(caller.Id, callerRole, item.Id, StatusId(project.Id, "In Review"));

        Assert.Equal("In Review", result.StatusName);
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_a_caller_who_is_neither_creator_assignee_nor_manager_or_admin()
    {
        var creator = AddUser("status-creator2@example.com");
        var stranger = AddUser("status-stranger@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToEditWorkItemException>(
            () => sut.UpdateStatusAsync(stranger.Id, "Developer", item.Id, StatusId(project.Id, "Done")));
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_a_statusId_belonging_to_a_different_project()
    {
        var creator = AddUser("status-invalid@example.com");
        var project = AddProject("Alpha", creator.Id);
        var otherProject = AddProject("Beta", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidWorkItemStatusException>(
            () => sut.UpdateStatusAsync(creator.Id, "Developer", item.Id, StatusId(otherProject.Id, "Done")));
    }

    [Fact]
    public async Task UpdateStatusAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(
            () => sut.UpdateStatusAsync(1, "Admin", 999999, 1));
    }

    [Theory]
    [InlineData(true, "Developer")] // creator
    [InlineData(false, "Manager")]
    [InlineData(false, "Admin")]
    public async Task DeleteAsync_allows_creator_or_manager_or_admin(bool asCreator, string callerRole)
    {
        var creator = AddUser("creator3@example.com");
        var caller = asCreator ? creator : AddUser($"deleter-{callerRole}@example.com", Enum.Parse<Role>(callerRole));
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await sut.DeleteAsync(caller.Id, callerRole, item.Id);

        Assert.Empty(Db.WorkItems);
    }

    [Fact]
    public async Task DeleteAsync_rejects_the_current_assignee_who_is_not_also_creator_manager_or_admin()
    {
        var creator = AddUser("creator4@example.com");
        var assignee = AddUser("assignee2@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id, assigneeUserId: assignee.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToDeleteWorkItemException>(
            () => sut.DeleteAsync(assignee.Id, "Developer", item.Id));
    }

    [Fact]
    public async Task DeleteAsync_rejects_an_unrelated_caller()
    {
        var creator = AddUser("creator5@example.com");
        var stranger = AddUser("stranger2@example.com");
        var project = AddProject("Alpha", creator.Id);
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToDeleteWorkItemException>(
            () => sut.DeleteAsync(stranger.Id, "Developer", item.Id));
    }

    [Fact]
    public async Task DeleteAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(() => sut.DeleteAsync(1, "Admin", 999999));
    }

    [Fact]
    public async Task GetWorkItemsAsync_returns_the_paginated_shape_sorted_by_updated_at_descending()
    {
        var user = AddUser("lister@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Oldest", updatedAt: DateTime.UtcNow.AddMinutes(-2));
        AddWorkItem(project.Id, user.Id, title: "Newest", updatedAt: DateTime.UtcNow);
        AddWorkItem(project.Id, user.Id, title: "Middle", updatedAt: DateTime.UtcNow.AddMinutes(-1));
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, page: 1, pageSize: 20, statusId: null, type: null, priority: null, assigneeUserId: null, search: null);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal("Newest", result.Items[0].Title);
        Assert.Equal("Middle", result.Items[1].Title);
        Assert.Equal("Oldest", result.Items[2].Title);
    }

    [Fact]
    public async Task GetWorkItemsAsync_filters_by_status_type_priority_and_assignee_individually_and_combined()
    {
        var user = AddUser("filterer@example.com");
        var assignee = AddUser("assignee3@example.com");
        var project = AddProject("Alpha", user.Id);
        var match = AddWorkItem(project.Id, user.Id, assigneeUserId: assignee.Id, status: "In Progress", priority: WorkItemPriority.High, type: WorkItemType.Story, title: "Match");
        AddWorkItem(project.Id, user.Id, status: "To Do", priority: WorkItemPriority.Low, type: WorkItemType.Task, title: "NoMatch");
        var inProgressId = StatusId(project.Id, "In Progress");
        var sut = CreateSut();

        var byStatus = await sut.GetWorkItemsAsync(project.Id, 1, 20, inProgressId, null, null, null, null);
        var byType = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, "Story", null, null, null);
        var byPriority = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, "High", null, null);
        var byAssignee = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, assignee.Id, null);
        var combined = await sut.GetWorkItemsAsync(project.Id, 1, 20, inProgressId, "Story", "High", assignee.Id, null);

        Assert.Equal(match.Id, byStatus.Items.Single().Id);
        Assert.Equal(match.Id, byType.Items.Single().Id);
        Assert.Equal(match.Id, byPriority.Items.Single().Id);
        Assert.Equal(match.Id, byAssignee.Items.Single().Id);
        Assert.Equal(match.Id, combined.Items.Single().Id);
    }

    [Fact]
    public async Task GetWorkItemsAsync_search_is_a_case_insensitive_title_substring_match()
    {
        var user = AddUser("searcher@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Fix the LOGIN bug");
        AddWorkItem(project.Id, user.Id, title: "Unrelated item");
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, null, search: "login");

        Assert.Equal("Fix the LOGIN bug", result.Items.Single().Title);
    }

    [Fact]
    public async Task GetWorkItemsAsync_clamps_a_pageSize_beyond_100_rather_than_rejecting_it()
    {
        var user = AddUser("clamper@example.com");
        var project = AddProject("Alpha", user.Id);
        for (var i = 0; i < 5; i++)
        {
            AddWorkItem(project.Id, user.Id, title: $"Item {i}");
        }
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, pageSize: 500, null, null, null, null, null);

        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetWorkItemsAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(
            () => sut.GetWorkItemsAsync(999999, 1, 20, null, null, null, null, null));
    }

    [Fact]
    public async Task CreateAsync_accepts_InReview_as_a_status()
    {
        var user = AddUser("inreview-create@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();
        var request = ValidRequest();
        request.StatusId = StatusId(project.Id, "In Review");

        var result = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal("In Review", result.StatusName);
    }

    [Fact]
    public async Task UpdateAsync_accepts_InReview_as_a_status()
    {
        var user = AddUser("inreview-update@example.com");
        var project = AddProject("Alpha", user.Id);
        var item = AddWorkItem(project.Id, user.Id, status: "In Progress");
        var sut = CreateSut();

        var result = await sut.UpdateAsync(user.Id, "Developer", item.Id, EditRequest(StatusId(project.Id, "In Review")));

        Assert.Equal("In Review", result.StatusName);
    }

    [Fact]
    public async Task GetWorkItemsAsync_filters_by_InReview_status()
    {
        var user = AddUser("inreview-filter@example.com");
        var project = AddProject("Alpha", user.Id);
        var match = AddWorkItem(project.Id, user.Id, status: "In Review", title: "In review item");
        AddWorkItem(project.Id, user.Id, status: "In Progress", title: "Not in review");
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, 20, StatusId(project.Id, "In Review"), null, null, null, null);

        Assert.Equal(match.Id, result.Items.Single().Id);
    }

    // Feature 006 — a statusId with no matching row just returns an empty result,
    // the same behavior an unmatched assigneeUserId filter already has; there is no
    // "unparseable status string" concept anymore now that status is identity-based.
    [Fact]
    public async Task GetWorkItemsAsync_returns_empty_for_a_statusId_that_matches_nothing()
    {
        var user = AddUser("badfilter@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Some item");
        var sut = CreateSut();

        var result = await sut.GetWorkItemsAsync(project.Id, 1, 20, 999999, null, null, null, null);

        Assert.Empty(result.Items);
    }

    // User Story 5 (non-regression): Feature 002's flat filter/search/pagination
    // logic is completely untouched by this feature — this test just proves it,
    // against a project that now contains a full hierarchy chain plus a standalone
    // item, confirming matches are returned regardless of depth or tree position.
    [Fact]
    public async Task GetWorkItemsAsync_returns_matches_regardless_of_hierarchy_position()
    {
        var user = AddUser("regression-hierarchy@example.com");
        var project = AddProject("Alpha", user.Id);
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Epic root");
        var story = AddChildWorkItem(project.Id, epic.Id, user.Id, WorkItemType.Story, "Story child", "In Progress");
        var task = AddChildWorkItem(project.Id, story.Id, user.Id, WorkItemType.Task, "Task grandchild");
        AddChildWorkItem(project.Id, task.Id, user.Id, WorkItemType.SubTask, "Deeply nested subtask", "Done");
        AddWorkItem(project.Id, user.Id, type: WorkItemType.Task, title: "Standalone done task", status: "Done");
        var doneId = StatusId(project.Id, "Done");
        var sut = CreateSut();

        var byStatus = await sut.GetWorkItemsAsync(project.Id, 1, 20, doneId, null, null, null, null);
        var bySearch = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, null, "nested");
        var all = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, null, null);

        Assert.Equal(2, byStatus.TotalCount);
        Assert.Equal("Deeply nested subtask", bySearch.Items.Single().Title);
        Assert.Equal(5, all.TotalCount);
    }

    // Feature 008 — a sprint isn't created through WorkItemService, so tests here seed
    // it directly, the same way AddStatus/AddWorkItem below already do for their own
    // entities.
    private Sprint AddSprint(int projectId, string name, DateTime start, DateTime end, SprintStatus status = SprintStatus.Planned)
    {
        var sprint = new Sprint { ProjectId = projectId, Name = name, StartDate = start, EndDate = end, Status = status };
        Db.Sprints.Add(sprint);
        Db.SaveChanges();
        return sprint;
    }

    [Fact]
    public async Task GetBacklogAsync_groups_items_into_sprint_sections_soonest_first_plus_a_backlog_section()
    {
        var user = AddUser("backlog-grouping@example.com");
        var project = AddProject("Alpha", user.Id);
        var later = AddSprint(project.Id, "Sprint 2", DateTime.UtcNow.Date.AddDays(20), DateTime.UtcNow.Date.AddDays(34));
        var sooner = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var inSooner = AddWorkItem(project.Id, user.Id, title: "In sprint 1");
        inSooner.SprintId = sooner.Id;
        var inLater = AddWorkItem(project.Id, user.Id, title: "In sprint 2");
        inLater.SprintId = later.Id;
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic, title: "Context epic");
        var unscheduled = AddWorkItem(project.Id, user.Id, title: "Unscheduled task");
        Db.SaveChanges();
        var sut = CreateSut();

        var backlog = await sut.GetBacklogAsync(project.Id, null, null, null, null, null, null);

        Assert.Equal(new[] { sooner.Id, later.Id }, backlog.Sprints.Select(s => s.Id));
        Assert.Equal("In sprint 1", backlog.Sprints[0].Items.Single().Title);
        Assert.Equal("In sprint 2", backlog.Sprints[1].Items.Single().Title);
        Assert.Equal(
            new[] { "Context epic", "Unscheduled task" },
            backlog.BacklogItems.Select(i => i.Title).OrderBy(t => t).ToArray());
    }

    [Fact]
    public async Task GetBacklogAsync_applies_the_same_filters_to_every_section()
    {
        var user = AddUser("backlog-filters@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var highInSprint = AddWorkItem(project.Id, user.Id, priority: WorkItemPriority.High, title: "High in sprint");
        highInSprint.SprintId = sprint.Id;
        var lowInSprint = AddWorkItem(project.Id, user.Id, priority: WorkItemPriority.Low, title: "Low in sprint");
        lowInSprint.SprintId = sprint.Id;
        AddWorkItem(project.Id, user.Id, priority: WorkItemPriority.High, title: "High unscheduled");
        AddWorkItem(project.Id, user.Id, priority: WorkItemPriority.Low, title: "Low unscheduled");
        Db.SaveChanges();
        var sut = CreateSut();

        var backlog = await sut.GetBacklogAsync(project.Id, null, null, "High", null, null, null);

        Assert.Equal("High in sprint", backlog.Sprints.Single().Items.Single().Title);
        Assert.Equal("High unscheduled", backlog.BacklogItems.Single().Title);
    }

    [Fact]
    public async Task GetBacklogAsync_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.GetBacklogAsync(999999, null, null, null, null, null, null));
    }

    [Fact]
    public async Task CreateAsync_and_UpdateAsync_reject_a_sprint_from_a_different_project()
    {
        var user = AddUser("sprint-cross-project@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherProject = AddProject("Beta", user.Id);
        var otherSprint = AddSprint(otherProject.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();
        var createRequest = RequestOfType("Task");
        createRequest.SprintId = otherSprint.Id;

        await Assert.ThrowsAsync<SprintNotFoundException>(() => sut.CreateAsync(user.Id, project.Id, createRequest));

        var item = AddWorkItem(project.Id, user.Id);
        var updateRequest = RequestOfType("Task");
        updateRequest.SprintId = otherSprint.Id;
        await Assert.ThrowsAsync<SprintNotFoundException>(() => sut.UpdateAsync(user.Id, "Admin", item.Id, updateRequest));
    }

    [Fact]
    public async Task CreateAsync_and_UpdateAsync_reject_an_Epic_assigned_to_a_sprint()
    {
        var user = AddUser("sprint-epic@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();
        var createRequest = RequestOfType("Epic");
        createRequest.SprintId = sprint.Id;

        await Assert.ThrowsAsync<EpicCannotBeInSprintException>(() => sut.CreateAsync(user.Id, project.Id, createRequest));

        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic);
        var updateRequest = RequestOfType("Epic");
        updateRequest.SprintId = sprint.Id;
        await Assert.ThrowsAsync<EpicCannotBeInSprintException>(() => sut.UpdateAsync(user.Id, "Admin", epic.Id, updateRequest));
    }

    [Fact]
    public async Task UpdateAsync_rejects_assigning_to_or_clearing_from_a_Completed_sprint()
    {
        var user = AddUser("sprint-readonly@example.com");
        var project = AddProject("Alpha", user.Id);
        var completedSprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date.AddDays(-14), DateTime.UtcNow.Date, SprintStatus.Completed);
        var sut = CreateSut();

        var unassigned = AddWorkItem(project.Id, user.Id);
        var assignRequest = RequestOfType("Task");
        assignRequest.SprintId = completedSprint.Id;
        await Assert.ThrowsAsync<SprintReadOnlyException>(() => sut.UpdateAsync(user.Id, "Admin", unassigned.Id, assignRequest));

        var alreadyInCompleted = AddWorkItem(project.Id, user.Id);
        alreadyInCompleted.SprintId = completedSprint.Id;
        Db.SaveChanges();
        var clearRequest = RequestOfType("Task");
        await Assert.ThrowsAsync<SprintReadOnlyException>(() => sut.UpdateAsync(user.Id, "Admin", alreadyInCompleted.Id, clearRequest));
    }

    [Fact]
    public async Task CreateAsync_assigns_a_valid_sprint_and_it_is_reflected_in_the_returned_dto()
    {
        var user = AddUser("sprint-assign-valid@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var sut = CreateSut();
        var request = RequestOfType("Task");
        request.SprintId = sprint.Id;

        var created = await sut.CreateAsync(user.Id, project.Id, request);

        Assert.Equal(sprint.Id, created.SprintId);
        Assert.Equal("Sprint 1", created.SprintName);
    }

    // ---------------------------------------------------------------------
    // US3 — UpdateSprintAsync (the Backlog view's drag interaction)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateSprintAsync_sets_the_sprint_for_a_caller_with_edit_rights()
    {
        var user = AddUser("dragsprint-set@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var item = AddWorkItem(project.Id, user.Id);
        var sut = CreateSut();

        var result = await sut.UpdateSprintAsync(user.Id, "Developer", item.Id, sprint.Id);

        Assert.Equal(sprint.Id, result.SprintId);
    }

    [Fact]
    public async Task UpdateSprintAsync_clears_the_sprint_when_given_null()
    {
        var user = AddUser("dragsprint-clear@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var item = AddWorkItem(project.Id, user.Id);
        item.SprintId = sprint.Id;
        Db.SaveChanges();
        var sut = CreateSut();

        var result = await sut.UpdateSprintAsync(user.Id, "Developer", item.Id, null);

        Assert.Null(result.SprintId);
    }

    [Fact]
    public async Task UpdateSprintAsync_rejects_a_caller_without_edit_rights()
    {
        var creator = AddUser("dragsprint-creator@example.com");
        var stranger = AddUser("dragsprint-stranger@example.com");
        var project = AddProject("Alpha", creator.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var item = AddWorkItem(project.Id, creator.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotAuthorizedToEditWorkItemException>(
            () => sut.UpdateSprintAsync(stranger.Id, "Developer", item.Id, sprint.Id));
    }

    [Fact]
    public async Task UpdateSprintAsync_rejects_an_Epic()
    {
        var user = AddUser("dragsprint-epic@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var epic = AddWorkItem(project.Id, user.Id, type: WorkItemType.Epic);
        var sut = CreateSut();

        await Assert.ThrowsAsync<EpicCannotBeInSprintException>(
            () => sut.UpdateSprintAsync(user.Id, "Admin", epic.Id, sprint.Id));
    }

    [Fact]
    public async Task UpdateSprintAsync_rejects_a_sprint_from_a_different_project()
    {
        var user = AddUser("dragsprint-crossproject@example.com");
        var project = AddProject("Alpha", user.Id);
        var otherProject = AddProject("Beta", user.Id);
        var otherSprint = AddSprint(otherProject.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var item = AddWorkItem(project.Id, user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintNotFoundException>(
            () => sut.UpdateSprintAsync(user.Id, "Admin", item.Id, otherSprint.Id));
    }

    [Fact]
    public async Task UpdateSprintAsync_rejects_moving_into_or_out_of_a_Completed_sprint()
    {
        var user = AddUser("dragsprint-readonly@example.com");
        var project = AddProject("Alpha", user.Id);
        var completedSprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date.AddDays(-14), DateTime.UtcNow.Date, SprintStatus.Completed);
        var unassigned = AddWorkItem(project.Id, user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<SprintReadOnlyException>(
            () => sut.UpdateSprintAsync(user.Id, "Admin", unassigned.Id, completedSprint.Id));

        var alreadyInCompleted = AddWorkItem(project.Id, user.Id);
        alreadyInCompleted.SprintId = completedSprint.Id;
        Db.SaveChanges();
        await Assert.ThrowsAsync<SprintReadOnlyException>(
            () => sut.UpdateSprintAsync(user.Id, "Admin", alreadyInCompleted.Id, null));
    }

    [Fact]
    public async Task UpdateSprintAsync_throws_for_an_unknown_work_item()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<WorkItemNotFoundException>(() => sut.UpdateSprintAsync(1, "Admin", 999999, null));
    }

    // ---------------------------------------------------------------------
    // US5 — GetBoardAsync's sprintId filter
    // ---------------------------------------------------------------------

    [Fact]
    public async Task GetBoardAsync_with_a_sprintId_returns_only_that_sprints_items_but_all_columns()
    {
        var user = AddUser("board-sprintfilter@example.com");
        var project = AddProject("Alpha", user.Id);
        var sprint = AddSprint(project.Id, "Sprint 1", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(14));
        var inSprint = AddWorkItem(project.Id, user.Id, title: "In sprint");
        inSprint.SprintId = sprint.Id;
        AddWorkItem(project.Id, user.Id, title: "Not in sprint");
        Db.SaveChanges();
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id, sprint.Id);

        Assert.Equal(4, board.Columns.Count);
        Assert.Equal("In sprint", board.Items.Single().Title);
    }

    [Fact]
    public async Task GetBoardAsync_without_a_sprintId_returns_every_item_unchanged()
    {
        var user = AddUser("board-nosprintfilter@example.com");
        var project = AddProject("Alpha", user.Id);
        AddWorkItem(project.Id, user.Id, title: "Item A");
        AddWorkItem(project.Id, user.Id, title: "Item B");
        var sut = CreateSut();

        var board = await sut.GetBoardAsync(project.Id);

        Assert.Equal(2, board.Items.Count);
    }
}
