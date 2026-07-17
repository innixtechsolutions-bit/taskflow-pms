using Microsoft.Extensions.Caching.Memory;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Tests.Services;

public class LoginAttemptTrackerTests
{
    private static LoginAttemptTracker CreateSut() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void IsBlocked_is_false_before_any_failures()
    {
        var sut = CreateSut();

        Assert.False(sut.IsBlocked("ada@example.com"));
    }

    [Fact]
    public void IsBlocked_is_false_after_four_failures_and_true_after_a_fifth()
    {
        var sut = CreateSut();
        for (var i = 0; i < 4; i++)
        {
            sut.RecordFailure("ada@example.com");
        }
        Assert.False(sut.IsBlocked("ada@example.com"));

        sut.RecordFailure("ada@example.com"); // 5th failure — the 6th attempt is now blocked

        Assert.True(sut.IsBlocked("ada@example.com"));
    }

    [Fact]
    public void RecordSuccess_clears_the_counter()
    {
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
        {
            sut.RecordFailure("ada@example.com");
        }
        Assert.True(sut.IsBlocked("ada@example.com"));

        sut.RecordSuccess("ada@example.com");

        Assert.False(sut.IsBlocked("ada@example.com"));
    }

    [Fact]
    public void Tracks_each_email_independently()
    {
        var sut = CreateSut();
        for (var i = 0; i < 5; i++)
        {
            sut.RecordFailure("ada@example.com");
        }

        Assert.False(sut.IsBlocked("grace@example.com"));
    }
}
