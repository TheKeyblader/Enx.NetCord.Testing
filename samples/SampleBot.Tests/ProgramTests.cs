using Xunit;

namespace SampleBot.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Main_InEmptySample_Completes()
    {
        Program.Main();
    }
}
