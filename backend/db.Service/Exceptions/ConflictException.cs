namespace db.Service.Exceptions;

/// <summary>
/// Throw this when a request would violate a uniqueness rule (e.g. a
/// username or email that's already taken). The middleware maps this to a
/// 409 Conflict response.
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
