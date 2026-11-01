using System.Text.Json.Serialization;

namespace Gocgun.Results.Http;

/// <summary>JSON body for a successful page. <see cref="Total"/> is omitted when it was not computed.</summary>
/// <typeparam name="T">Row type.</typeparam>
public sealed class PaginatedHttpBody<T>
{
    /// <summary>Gets the page rows.</summary>
    public required IReadOnlyList<T> Item { get; init; }

    /// <summary>Gets the total row count when the caller computed it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Total { get; init; }
}
