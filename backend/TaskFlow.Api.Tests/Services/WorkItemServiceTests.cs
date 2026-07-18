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
        return project;
    }

    private WorkItem AddWorkItem(
        int projectId, int creatorId, int? assigneeUserId = null,
        WorkItemStatus status = WorkItemStatus.ToDo, WorkItemPriority priority = WorkItemPriority.Medium,
        WorkItemType type = WorkItemType.Task, string title = "Some work", DateTime? updatedAt = null)
    {
        var workItem = new WorkItem
        {
            ProjectId = projectId,
            Type = type,
            Title = title,
            Status = status,
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
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id
        };
        Db.WorkItems.Add(story);
        Db.SaveChanges();
        var task = new WorkItem
        {
            ProjectId = project.Id, Type = WorkItemType.Task, Title = "Task", CreatedByUserId = user.Id,
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
        for (var i = 0; i < 5; i++)
        {
            Db.WorkItems.Add(new WorkItem
            {
                ProjectId = project.Id, Type = WorkItemType.Story, Title = $"Story {i}", CreatedByUserId = user.Id,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = epic.Id,
                Status = i < 3 ? WorkItemStatus.Done : WorkItemStatus.ToDo
            });
        }
        Db.SaveChanges();
        var sut = CreateSut();

        var tree = await sut.GetTreeAsync(project.Id);

        var epicNode = Assert.Single(tree);
        Assert.Equal(5, epicNode.DirectChildrenCount);
        Assert.Equal(3, epicNode.DirectChildrenDoneCount);
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

    private WorkItem AddChildWorkItem(int projectId, int parentId, int creatorId, WorkItemType type, string title, WorkItemStatus status = WorkItemStatus.ToDo, int? assigneeUserId = null)
    {
        var workItem = new WorkItem
        {
            ProjectId = projectId, Type = type, Title = title, CreatedByUserId = creatorId,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ParentWorkItemId = parentId,
            Status = status, AssigneeUserId = assigneeUserId
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
        Assert.Equal("ToDo", result.Status);
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

    private static WorkItemRequest EditRequest(string title = "Updated title", string status = "Done") => new()
    {
        Type = "Task",
        Title = title,
        Status = status
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

        var result = await sut.UpdateAsync(caller.Id, callerRole, item.Id, EditRequest());

        Assert.Equal("Updated title", result.Title);
        Assert.Equal("Done", result.Status);
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

        var result = await sut.GetWorkItemsAsync(project.Id, page: 1, pageSize: 20, status: null, type: null, priority: null, assigneeUserId: null, search: null);

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
        var match = AddWorkItem(project.Id, user.Id, assigneeUserId: assignee.Id, status: WorkItemStatus.InProgress, priority: WorkItemPriority.High, type: WorkItemType.Story, title: "Match");
        AddWorkItem(project.Id, user.Id, status: WorkItemStatus.ToDo, priority: WorkItemPriority.Low, type: WorkItemType.Task, title: "NoMatch");
        var sut = CreateSut();

        var byStatus = await sut.GetWorkItemsAsync(project.Id, 1, 20, "InProgress", null, null, null, null);
        var byType = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, "Story", null, null, null);
        var byPriority = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, "High", null, null);
        var byAssignee = await sut.GetWorkItemsAsync(project.Id, 1, 20, null, null, null, assignee.Id, null);
        var combined = await sut.GetWorkItemsAsync(project.Id, 1, 20, "InProgress", "Story", "High", assignee.Id, null);

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
    public async Task GetWorkItemsAsync_throws_for_an_unparseable_status_filter()
    {
        var user = AddUser("badfilter@example.com");
        var project = AddProject("Alpha", user.Id);
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidWorkItemStatusException>(
            () => sut.GetWorkItemsAsync(project.Id, 1, 20, "NotAStatus", null, null, null, null));
    }
}
