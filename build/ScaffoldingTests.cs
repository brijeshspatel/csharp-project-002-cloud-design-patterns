using System.Reflection;

namespace PatternScaffolding;

/// <summary>
/// Compiled into every test project from a single source file, so there is one
/// copy to change rather than twenty-three.
///
/// This asserts what the test scaffolding itself delivers: that each test
/// assembly really is wired to the pattern it names, and that the pattern
/// exposes something a test could reach. It is not a substitute for the tests
/// that exercise the pattern -- those belong with each pattern -- but without it
/// a test project whose reference had been removed would report success while
/// testing nothing at all.
/// </summary>
public class ScaffoldingTests
{
    private static string PatternAssemblyName =>
        typeof(ScaffoldingTests).Assembly.GetName().Name!.Replace(
            ".Tests", string.Empty, StringComparison.Ordinal);

    [Fact]
    public void The_test_assembly_can_load_the_pattern_it_is_named_for()
    {
        Assembly pattern = Assembly.Load(new AssemblyName(PatternAssemblyName));

        Assert.Equal(PatternAssemblyName, pattern.GetName().Name);
    }

    [Fact]
    public void The_pattern_exposes_at_least_one_public_type()
    {
        // A pattern whose participants are all internal cannot be tested from
        // outside, which would make its test project permanently empty for a
        // reason no failing test would ever report.
        Assembly pattern = Assembly.Load(new AssemblyName(PatternAssemblyName));

        Assert.NotEmpty(pattern.GetExportedTypes());
    }
}
