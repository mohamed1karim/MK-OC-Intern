namespace db.Service.Exceptions;

/// <summary>
/// Throw this when the acting user's role doesn't allow the action they're
/// trying to do (e.g. a non-Admin trying to complete/cancel an order). The
/// middleware maps this to a 403 Forbidden response — distinct from 401
/// (which would mean "we don't know who you are"; we do know, they're just
/// not allowed to do this).
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
