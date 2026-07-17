using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Data.Entities;
using TaskFlow.Api.Dtos;

namespace TaskFlow.Api.Services;

public class UserService(AppDbContext dbContext)
{
    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(int page, int pageSize)
    {
        var query = dbContext.Users.OrderBy(u => u.Id);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto(u.Id, u.FullName, u.Email, u.Role.ToString(), u.CreatedAt))
            .ToListAsync();

        return new PagedResult<UserListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<UserListItemDto> ChangeRoleAsync(int callerId, int targetUserId, string newRoleRaw)
    {
        if (!Enum.TryParse<Role>(newRoleRaw, ignoreCase: true, out var newRole))
        {
            throw new InvalidRoleException();
        }

        var target = await dbContext.Users.FindAsync(targetUserId) ?? throw new UserNotFoundException();

        // Self-demotion guard (FR-016), evaluated immediately before commit so a
        // concurrent request can't slip a second demotion through between the count
        // check and the save. Scoped to self-demotion only — see LastAdminException.
        if (target.Id == callerId && target.Role == Role.Admin && newRole != Role.Admin)
        {
            var adminCount = await dbContext.Users.CountAsync(u => u.Role == Role.Admin);
            if (adminCount <= 1)
            {
                throw new LastAdminException();
            }
        }

        target.Role = newRole;
        await dbContext.SaveChangesAsync();

        return new UserListItemDto(target.Id, target.FullName, target.Email, target.Role.ToString(), target.CreatedAt);
    }

    // Backs the work-item assignee picker (Feature 002) — any authenticated user can
    // call this, so it returns only id + full name, never email/role/registration date
    // the way GetUsersAsync (Admin-only) does.
    public async Task<List<UserLookupItemDto>> GetAssignableUsersAsync() =>
        await dbContext.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserLookupItemDto(u.Id, u.FullName))
            .ToListAsync();
}
