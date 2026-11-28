using System.Globalization;
using System.Net;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

internal static class ChannelsTestSupport
{
    public static string Raw(ulong id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Asserts the status and Discord code NetCord parsed from an error response.</summary>
    public static void AssertError(RestException exception, HttpStatusCode status, int code)
    {
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(code, exception.Error!.Code);
    }
}
