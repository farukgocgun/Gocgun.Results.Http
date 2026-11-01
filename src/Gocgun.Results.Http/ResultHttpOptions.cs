namespace Gocgun.Results.Http;

/// <summary>Settings for the problem responses written by <see cref="ResultHttpExtensions"/>.</summary>
public sealed class ResultHttpOptions
{
    /// <summary>
    /// Gets or sets the base URI of the error catalog. The problem <c>type</c> becomes this base plus the kebab-case error code.
    /// When null, <c>type</c> is <c>about:blank</c> as RFC 9457 defines for problems without a dedicated type.
    /// </summary>
    public Uri? ErrorTypeBase { get; set; }
}
