namespace TalepPazari.ArchitectureTests.Support;

internal static class ArchAssert
{
    // Fails with the rule and every offending project or type, one per line.
    public static void NoViolations(string rule, IEnumerable<string> violations)
    {
        var list = violations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        Assert.True(list.Count == 0, $"Rule: {rule}{Environment.NewLine}Violations ({list.Count}):{Environment.NewLine}  - "
            + string.Join(Environment.NewLine + "  - ", list));
    }

    // Guard (rule 12): a rule that runs against nothing would pass without checking anything.
    public static IReadOnlyList<T> NotEmpty<T>(IEnumerable<T> subjects, string description)
    {
        var list = subjects.ToList();
        Assert.True(list.Count > 0, $"Guard: no {description} found, so the rule would pass without checking anything. "
            + "Check module discovery or the rule's selection.");
        return list;
    }
}
