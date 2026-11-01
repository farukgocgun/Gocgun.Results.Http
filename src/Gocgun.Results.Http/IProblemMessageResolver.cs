namespace Gocgun.Results.Http;

/// <summary>Optional hook that turns an <see cref="Error.Message"/> (often a catalog key) into the text written to the problem <c>detail</c>.</summary>
public interface IProblemMessageResolver
{
    /// <summary>Resolves <paramref name="message"/> for the current request culture.</summary>
    /// <param name="message">The message carried by the error.</param>
    /// <returns>The text for the client. Return <paramref name="message"/> when there is nothing to resolve.</returns>
    string Resolve(string message);
}
