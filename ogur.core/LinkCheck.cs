using System;

namespace Ogur.Core;

/// <summary>
/// Provides lightweight URI validation helpers.
/// </summary>
public static class LinkCheck
{
    /// <summary>
    /// Checks whether the given string can be parsed as an absolute HTTP/HTTPS URI.
    /// </summary>
    /// <param name="value">String value to validate.</param>
    /// <returns><c>true</c> if the URI is absolute and uses HTTP/HTTPS scheme; otherwise <c>false</c>.</returns>
    public static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}