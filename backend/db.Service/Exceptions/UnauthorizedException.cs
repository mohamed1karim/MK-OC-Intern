namespace db.Service.Exceptions;

/// <summary>
/// Throw this when a login attempt's username/password don't match. The
/// middleware maps this to a 401 Unauthorized response.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
