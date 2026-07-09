// A custom exception type. Instead of every service method manually building
// "404 not found" responses, a service just throws this when it can't find
// something, and one shared middleware (see db.api/Middleware/ExceptionHandlingMiddleware.cs)
// catches it and turns it into the correct HTTP response automatically.

namespace db.Service.Exceptions;

/// <summary>
/// Throw this when a lookup by id (or similar) finds nothing. The middleware
/// maps this to a 404 Not Found response.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
