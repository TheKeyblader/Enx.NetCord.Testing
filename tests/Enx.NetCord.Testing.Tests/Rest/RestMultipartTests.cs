using System.Text;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RestMultipartTests
{
    private static RestRequestBody Body(string text, string? contentType = "multipart/form-data")
        => new(Encoding.Latin1.GetBytes(text), contentType);

    [Fact]
    public void TryParse_PayloadAndFile_ReturnsThePartsWithTheirExactBytes()
    {
        var text = "--b\r\nContent-Type: application/json\r\nContent-Disposition: form-data; name=payload_json\r\n\r\n{\"content\":\"hi\"}\r\n"
            + "--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"a b.txt\"; filename*=utf-8''a%20b.txt\r\nContent-Type: text/plain\r\n\r\n\u0001ÿ\r\nraw\r\n--b--\r\n";

        Assert.True(RestMultipart.TryParse(Body(text), out var parts));

        Assert.Equal(2, parts.Count);
        Assert.Equal("payload_json", parts[0].Name);
        Assert.Null(parts[0].FileName);
        Assert.Equal("{\"content\":\"hi\"}", parts[0].Text);
        Assert.Equal("files[0]", parts[1].Name);
        Assert.Equal("a b.txt", parts[1].FileName);
        Assert.Equal("text/plain", parts[1].ContentType);
        Assert.Equal(new byte[] { 1, 0xff, (byte)'\r', (byte)'\n', (byte)'r', (byte)'a', (byte)'w' }, parts[1].Content);
    }

    [Fact]
    public void TryParse_EncodedWordFileName_IsDecodedAsUtf8()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("é.txt"));
        var text = $"--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"=?utf-8?B?{encoded}?=\"\r\n\r\nx\r\n--b--\r\n";

        Assert.True(RestMultipart.TryParse(Body(text), out var parts));

        Assert.Equal("é.txt", Assert.Single(parts).FileName);
    }

    [Fact]
    public void TryParse_QuotedNameWithASemicolon_IsKeptWhole()
    {
        var text = "--b\r\nContent-Disposition: form-data; name=\"a;b\"\r\n\r\nv\r\n--b--\r\n";

        Assert.True(RestMultipart.TryParse(Body(text), out var parts));

        Assert.Equal("a;b", Assert.Single(parts).Name);
    }

    [Theory]
    [InlineData("not multipart")]
    [InlineData("--b\r\nContent-Disposition: form-data\r\n\r\nv\r\n--b--\r\n")]
    [InlineData("--b\r\nno header end")]
    public void TryParse_Malformed_ReturnsFalse(string text)
    {
        Assert.False(RestMultipart.TryParse(Body(text), out var parts));
        Assert.Null(parts);
    }

    [Fact]
    public void TryParse_NotAMultipartContentType_ReturnsFalse()
    {
        Assert.False(RestMultipart.IsMultipart(Body("{}", "application/json")));
        Assert.False(RestMultipart.TryParse(Body("--b\r\n", "application/json"), out _));
    }
}
