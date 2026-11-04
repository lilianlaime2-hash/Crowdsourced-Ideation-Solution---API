using CIS.Migration;
namespace CIS.Migration.Tests;

public class CliTests
{
    [Fact]
    public async Task UnknownCommandFailsWithUsageExitCode() => Assert.Equal(2, await Program.Main(["unknown"]));
}
