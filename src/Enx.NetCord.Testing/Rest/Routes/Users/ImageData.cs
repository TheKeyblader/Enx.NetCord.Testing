using System.Security.Cryptography;

namespace Enx.NetCord.Testing.Rest.Routes.Users;

/// <summary>Reads Discord "image data" (<c>data:image/png;base64,...</c>) and derives the hash Discord would store.</summary>
internal static class ImageData
{
    private const string Prefix = "data:image/";
    private const string Base64Marker = ";base64,";

    /// <summary>
    /// Validates an image data URI and derives a deterministic asset hash from its bytes: 32 lowercase hex digits
    /// (the shape of a Discord hash), prefixed with <c>a_</c> for a GIF. The image content itself is not decoded.
    /// </summary>
    /// <param name="value">The value sent by the client.</param>
    /// <param name="hash">The asset hash when the value is valid.</param>
    /// <returns><see langword="true"/> when the value is a base64 PNG, JPEG (<c>jpeg</c> or NetCord's <c>jpg</c>), GIF or WebP data URI.</returns>
    public static bool TryGetHash(string value, out string hash)
    {
        hash = string.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var marker = value.IndexOf(Base64Marker, Prefix.Length, StringComparison.Ordinal);
        if (marker < 0)
        {
            return false;
        }

        var format = value[Prefix.Length..marker];
        // NetCord writes a JPEG as `jpg` (ImageUrl.GetFormatBytes), the docs say `jpeg`: both are accepted.
        if (format is not ("png" or "jpeg" or "jpg" or "gif" or "webp"))
        {
            return false;
        }

        var base64 = value.AsSpan(marker + Base64Marker.Length);
        var buffer = new byte[(base64.Length / 4 * 3) + 3];
        if (!Convert.TryFromBase64Chars(base64, buffer, out var written) || written == 0)
        {
            return false;
        }

        var digest = SHA256.HashData(buffer.AsSpan(0, written));
        var hex = Convert.ToHexStringLower(digest.AsSpan(0, 16));
        hash = format == "gif" ? "a_" + hex : hex;
        return true;
    }
}
