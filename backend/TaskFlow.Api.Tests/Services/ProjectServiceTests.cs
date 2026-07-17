using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class ProjectServiceTests : SqlServerTestDatabase
{
    private ProjectService CreateSut() => new(Db);

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

    private static ProjectRequest ValidRequest(string name = "Website Redesign") => new()
    {
        Name = name,
        Description = "Rebuild the marketing site"
    };

    [Fact]
    public async Task CreateAsync_creates_a_project_recording_creator_and_timestamp()
    {
        var manager = AddUser("manager@example.com", Role.Manager);
        var sut = CreateSut();
        var before = DateTime.UtcNow;

        var result = await sut.CreateAsync(manager.Id, ValidRequest());

        Assert.Equal("Website Redesign", result.Name);
        Assert.Equal("Test User", result.CreatedByName);
        Assert.True(result.CreatedAt >= before);
        Assert.Equal(0, result.TotalWorkItemCount);
        var stored = Db.Projects.Single();
        Assert.Equal(manager.Id, stored.CreatedByUserId);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_name_case_insensitively()
    {
        var manager = AddUser("manager@example.com", Role.Manager);
        var sut = CreateSut();
        await sut.CreateAsync(manager.Id, ValidRequest("Website Redesign"));

        var ex = await Assert.ThrowsAsync<DuplicateProjectNameException>(() =>
            sut.CreateAsync(manager.Id, ValidRequest("website redesign")));

        Assert.Equal("A project with this name already exists.", ex.Message);
    }
}
