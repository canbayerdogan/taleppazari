using Microsoft.Extensions.Logging;
using TalepPazari.IntegrationTests.Support;

namespace TalepPazari.ApiTests.Support;

// Shared checks of captured log entries (message, scope values and full exception text).
internal static class LogAssert
{
    public static void NoTextInAnyEntry(IEnumerable<LogEntry> entries, params string[] secrets)
    {
        var list = entries.ToList();
        Assert.NotEmpty(list);
        foreach (var secret in secrets)
        {
            Assert.All(list, entry => Assert.DoesNotContain(secret, entry.AllText, StringComparison.Ordinal));
        }
    }

    // No password and no connection-string fragment, at any level.
    public static void NoCredentialInAnyEntry(IEnumerable<LogEntry> entries, string password)
    {
        var list = entries.ToList();
        Assert.NotEmpty(list);
        Assert.All(list, entry =>
        {
            Assert.DoesNotContain(password, entry.AllText, StringComparison.Ordinal);
            Assert.DoesNotContain("Password=", entry.AllText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Username=", entry.AllText, StringComparison.OrdinalIgnoreCase);
        });
    }

    // The project rule (LogPolicy): no Warning+ entry of any category carries an exception object.
    public static void NoExceptionAtWarningOrAbove(IEnumerable<LogEntry> entries) =>
        Assert.All(entries.Where(entry => entry.Level >= LogLevel.Warning), entry =>
            Assert.True(entry.Exception is null, $"{entry.Category} logged an exception object at {entry.Level}."));
}
