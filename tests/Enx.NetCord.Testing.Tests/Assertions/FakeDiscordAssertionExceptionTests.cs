using Xunit;

namespace Enx.NetCord.Testing.Tests.Assertions;

public sealed class FakeDiscordAssertionExceptionTests
{
    [Fact]
    public void Type_IsASealedExceptionOfTheRootNamespace()
    {
        var type = typeof(FakeDiscordAssertionException);

        Assert.True(type.IsSealed);
        Assert.True(type.IsPublic);
        Assert.Equal("Enx.NetCord.Testing", type.Namespace);
        Assert.Same(typeof(Exception), type.BaseType);
    }

    [Fact]
    public void Type_ImplementsAnInterfaceNamedIAssertionException()
    {
        // xUnit v3 recognizes the cause of a failure by this simple name, whatever the namespace or visibility.
        var marker = Assert.Single(typeof(FakeDiscordAssertionException).GetInterfaces(), static i => i.Name == "IAssertionException");

        Assert.False(marker.IsPublic);
        Assert.Empty(marker.GetMembers());
    }

    [Fact]
    public void Constructor_Message_KeepsTheMessageAndHasNoInnerException()
    {
        var exception = new FakeDiscordAssertionException("nope");

        Assert.Equal("nope", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Constructor_MessageAndInnerException_KeepsBoth()
    {
        var inner = new InvalidOperationException("cause");

        var exception = new FakeDiscordAssertionException("nope", inner);

        Assert.Equal("nope", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Constructors_AreTheTwoPublicOnes()
    {
        Assert.Equal(2, typeof(FakeDiscordAssertionException).GetConstructors().Length);
    }
}
