using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DocuMind.IntegrationTests;

/// <summary>
/// Records the exact SQL text and bound parameters of the last command EF Core issued, so a test
/// can re-issue the real production query under <c>EXPLAIN ANALYZE</c> instead of hand-writing an
/// approximate one (ADR-K). Parameters are cloned via <see cref="NpgsqlParameter.Clone"/> rather
/// than reduced to name/value/type, so provider-specific details the pgvector type mapping depends
/// on (e.g. <see cref="NpgsqlParameter.DataTypeName"/>) survive the round trip unchanged.
/// </summary>
public sealed class CapturingCommandInterceptor : DbCommandInterceptor
{
    public string? LastCommandText { get; private set; }

    public List<NpgsqlParameter> LastParameters { get; } = [];

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Capture(DbCommand command)
    {
        LastCommandText = command.CommandText;
        LastParameters.Clear();

        foreach (var parameter in command.Parameters.OfType<NpgsqlParameter>())
        {
            LastParameters.Add((NpgsqlParameter)parameter.Clone());
        }
    }
}
