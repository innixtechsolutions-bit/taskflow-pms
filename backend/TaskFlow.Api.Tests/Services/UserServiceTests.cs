using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Services;
using TaskFlow.Api.Tests.TestSupport;

namespace TaskFlow.Api.Tests.Services;

public class UserServiceTests : SqlServerTestDatabase
{
    private UserService CreateSut() => new(Db);

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

    [Fact]
    public async Task GetUsersAsync_returns_the_paginated_shape()
    {
        AddUser("a@example.com");
        AddUser("b@example.com");
        AddUser("c@example.com");
        var sut = CreateSut();

        var result = await sut.GetUsersAsync(page: 1, pageSize: 2);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetUsersAsync_returns_the_second_page()
    {
        AddUser("a@example.com");
        AddUser("b@example.com");
        AddUser("c@example.com");
        var sut = CreateSut();

        var result = await sut.GetUsersAsync(page: 2, pageSize: 2);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ChangeRoleAsync_updates_the_target_users_role()
    {
        var target = AddUser("dev@example.com");
        var admin = AddUser("admin@example.com", Role.Admin);
        var sut = CreateSut();

        var updated = await sut.ChangeRoleAsync(admin.Id, target.Id, "Manager");

        Assert.Equal("Manager", updated.Role);
        Assert.Equal(Role.Manager, Db.Users.Single(u => u.Id == target.Id).Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_rejects_the_last_admin_demoting_themselves()
    {
        var admin = AddUser("solo-admin@example.com", Role.Admin);
        var sut = CreateSut();

        await Assert.ThrowsAsync<LastAdminException>(() =>
            sut.ChangeRoleAsync(admin.Id, admin.Id, "Developer"));
        Assert.Equal(Role.Admin, Db.Users.Single(u => u.Id == admin.Id).Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_allows_self_demotion_when_another_admin_exists()
    {
        var admin = AddUser("admin-one@example.com", Role.Admin);
        AddUser("admin-two@example.com", Role.Admin);
        var sut = CreateSut();

        var updated = await sut.ChangeRoleAsync(admin.Id, admin.Id, "Developer");

        Assert.Equal("Developer", updated.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_allows_demoting_a_different_admin_even_if_they_are_the_only_other_one()
    {
        var caller = AddUser("caller-admin@example.com", Role.Admin);
        var otherAdmin = AddUser("other-admin@example.com", Role.Admin);
        var sut = CreateSut();

        var updated = await sut.ChangeRoleAsync(caller.Id, otherAdmin.Id, "Developer");

        Assert.Equal("Developer", updated.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_throws_for_an_unknown_user_id()
    {
        var admin = AddUser("admin@example.com", Role.Admin);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            sut.ChangeRoleAsync(admin.Id, 999999, "Developer"));
    }

    [Fact]
    public async Task GetAssignableUsersAsync_returns_every_users_id_and_full_name_only()
    {
        var a = AddUser("a@example.com");
        var b = AddUser("b@example.com", Role.Manager);
        var sut = CreateSut();

        var result = await sut.GetAssignableUsersAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.Id == a.Id && u.FullName == "Test User");
        Assert.Contains(result, u => u.Id == b.Id);
    }
}
