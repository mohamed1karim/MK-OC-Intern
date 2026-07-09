// This middleware wraps every incoming request. Middleware in ASP.NET Core is
// just code that runs before/after the rest of the pipeline for every
// request — this one specifically watches for our custom exceptions
// (NotFoundException, ConflictException) and turns them into the correct
// HTTP status code + a clear JSON error body, instead of the controller
// having to remember to do that on every single action.
using System.Net;
using System.Text.Json;
using db.Service.Exceptions;

namespace db.api.Middleware;

public class ExceptionHandlingMiddleware
{
    // RequestDelegate represents "the rest of the pipeline" — everything
    // that would normally run after this middleware (routing, the
    // controller action, etc.). We call it inside a try/catch below.
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // ASP.NET Core calls this method automatically for every request.
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Let the request continue through the pipeline (eventually
            // reaching the controller). If a service method throws one of
            // our exceptions anywhere along the way, it bubbles up to here.
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            // The acting user is known, they just aren't allowed to do this
            // (e.g. a non-Admin trying to complete/cancel an order).
            await WriteErrorAsync(context, HttpStatusCode.Forbidden, ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            // A login attempt with a bad username/password — unlike
            // Forbidden, we don't know who this is yet.
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Thrown by things like an invalid Role string ("Manager" isn't
            // User or Admin) — a client input problem, so 400 Bad Request.
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Anything we didn't expect: log it and return a generic 500,
            // rather than leaking internal exception details to the client.
            Console.Error.WriteLine(ex);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred.");
        }
    }

    private static Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var body = JsonSerializer.Serialize(new { error = message });
        return context.Response.WriteAsync(body);
    }
}
