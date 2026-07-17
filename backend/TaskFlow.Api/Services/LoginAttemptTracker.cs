using Microsoft.Extensions.Caching.Memory;

namespace TaskFlow.Api.Services;

public interface ILoginAttemptTracker
{
    bool IsBlocked(string email);
    void RecordFailure(string email);
    void RecordSuccess(string email);
}

// Singleton (Program.cs), not Scoped: it wraps one shared IMemoryCache that must persist
// across requests to actually rate-limit anything — a Scoped instance would reset on
// every request and never accumulate failures. Keyed on email (not IP/connection), per
// FR-019, which blocks by account rather than by network origin.
public class LoginAttemptTracker(IMemoryCache cache) : ILoginAttemptTracker
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool IsBlocked(string email) =>
        cache.TryGetValue(Key(email), out int attempts) && attempts >= MaxAttempts;

    public void RecordFailure(string email)
    {
        var key = Key(email);
        var attempts = cache.TryGetValue(key, out int existing) ? existing + 1 : 1;
        cache.Set(key, attempts, new MemoryCacheEntryOptions { SlidingExpiration = Window });
    }

    public void RecordSuccess(string email) => cache.Remove(Key(email));

    private static string Key(string email) => $"login-attempts:{email.ToLowerInvariant()}";
}
