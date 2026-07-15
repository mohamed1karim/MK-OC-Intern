namespace db.Service.Ai;

public interface IProductDescriptionEnhancer
{
    // Turns a short, employee-written product description into a more
    // detailed one. Implementations must never throw for a failed AI call —
    // fall back to returning shortDescription unchanged instead, so product
    // creation never fails because the AI provider is down/misconfigured.
    Task<string> EnhanceAsync(string productName, string shortDescription, CancellationToken cancellationToken = default);
}
