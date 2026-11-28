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
        Assert.Equal(new byte[] { 1, 0xff, (byte)'\r', (byte)'\n', (byte)'r', (byte)'a', (byte)'w' }, parts[1].Content.ToArray());
        Assert.Equal(7, parts[1].Length);
    }

    [Fact]
    public void TryParse_Parts_AreSlicesOfTheBodyNotCopies()
    {
        var body = Body("--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"a.bin\"\r\n\r\nDATA\r\n--b--\r\n");

        Assert.True(RestMultipart.TryParse(body, out var parts));

        var part = Assert.Single(parts);
        Assert.True(part.Content.Span.Overlaps(body.Content.Span));
    }

    [Fact]
    public void TryParse_ClosingDelimiterAtTheEndOfTheBodyWithoutALineBreak_ParsesTheLastPart()
    {
        Assert.True(RestMultipart.TryParse(Body("--b\r\nContent-Disposition: form-data; name=a\r\n\r\nv\r\n--b--"), out var parts));

        Assert.Equal("v", Assert.Single(parts).Text);
    }

    [Fact]
    public void TryParse_NoClosingDelimiter_KeepsTheContentWithoutTheFinalLineBreak()
    {
        Assert.True(RestMultipart.TryParse(Body("--b\r\nContent-Disposition: form-data; name=a\r\n\r\nv\r\n"), out var parts));

        Assert.Equal("v", Assert.Single(parts).Text);
    }

    [Fact]
    public async Task TryParse_QuotedBoundaryFromTheHttpClient_ParsesTheParts()
    {
        using var content = new MultipartFormDataContent("my boundary");
        content.Add(new StringContent("{\"content\":\"hi\"}", Encoding.UTF8, "application/json"), "payload_json");
        content.Add(new ByteArrayContent([1, 2, 3]), "files[0]", "a.bin");
        var bytes = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains("boundary=\"my boundary\"", content.Headers.ContentType!.ToString(), StringComparison.Ordinal);

        Assert.True(RestMultipart.TryParse(new RestRequestBody(bytes, "multipart/form-data"), out var parts));

        Assert.Equal(["payload_json", "files[0]"], parts.Select(p => p.Name));
        Assert.Equal("{\"content\":\"hi\"}", parts[0].Text);
        Assert.Equal(new byte[] { 1, 2, 3 }, parts[1].Content.ToArray());
    }

    [Fact]
    public void TryParse_BinaryFileResemblingTheBoundary_IsKeptInItsPart()
    {
        // "--b" mid-line, "\r\n--bX" (a longer token) and "--b" at the start of a line after a bare LF are not delimiters.
        var file = "x--b\r\n--bX\n--b--y";
        var text = "--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"a.bin\"\r\n\r\n" + file + "\r\n--b--\r\n";

        Assert.True(RestMultipart.TryParse(Body(text), out var parts));

        var part = Assert.Single(parts);
        Assert.Equal(file, Encoding.Latin1.GetString(part.Content.Span));
    }

    [Fact]
    public void TryParse_BinaryFileWithNonAsciiBytes_KeepsEveryByte()
    {
        var bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var head = Encoding.Latin1.GetBytes("--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"a.bin\"\r\n\r\n");
        var tail = Encoding.Latin1.GetBytes("\r\n--b--\r\n");

        Assert.True(RestMultipart.TryParse(new RestRequestBody(head.Concat(bytes).Concat(tail).ToArray(), "multipart/form-data"), out var parts));

        Assert.Equal(bytes, Assert.Single(parts).Content.ToArray());
    }

    [Fact]
    public void TryParse_SeveralFiles_ReturnsThemInOrderWithTheirSizes()
    {
        var text = "--b\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"a\"\r\n\r\n12\r\n"
            + "--b\r\nContent-Disposition: form-data; name=\"files[1]\"; filename=\"b\"\r\n\r\n\r\n"
            + "--b\r\nContent-Disposition: form-data; name=\"files[2]\"; filename=\"c\"\r\n\r\n123\r\n--b--\r\n";

        Assert.True(RestMultipart.TryParse(Body(text), out var parts));

        Assert.Equal(["files[0]", "files[1]", "files[2]"], parts.Select(p => p.Name));
        Assert.Equal([2, 0, 3], parts.Select(p => p.Length));
    }

    [Fact]
    public void TryParse_Lenient_SkipsAMalformedSiblingPart()
    {
        var bytes = Encoding.Latin1.GetBytes("--b\r\nContent-Disposition: form-data\r\n\r\nv\r\n--b\r\nContent-Disposition: form-data; name=payload_json\r\n\r\n{}\r\n--b--\r\n");

        Assert.False(RestMultipart.TryParse(new RestRequestBody(bytes, "multipart/form-data"), out _));
        Assert.True(RestMultipart.TryParse(bytes, lenient: true, out var parts));

        Assert.Equal("payload_json", Assert.Single(parts).Name);
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
