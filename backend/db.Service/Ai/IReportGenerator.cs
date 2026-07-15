namespace db.Service.Ai;

public interface IReportGenerator
{
    // Turns a plain-text stats summary into a narrative report. Must never
    // throw — implementations should return a clear "unavailable" message on
    // any failure, since the caller still has the raw stats to show either way.
    Task<string> GenerateAsync(string statsSummary, CancellationToken cancellationToken = default);
}
