using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal static class SqlServerPantryConsumptionErrorClassifier
{
    private const string ConsumptionTableToken =
        "object 'pantry.StockConsumptions'";
    private const string IdempotencyIndexToken =
        "unique index '" +
        StockConsumptionConfiguration.UserIdempotencyKeyUniqueIndexName +
        "'";
    private const string IdempotencyConstraintToken =
        "constraint '" +
        StockConsumptionConfiguration.UserIdempotencyKeyUniqueIndexName +
        "'";

    internal static bool IsIdempotencyRace(DbUpdateException exception)
    {
        if (exception.Entries.Count != 1 ||
            exception.Entries[0].Entity is not StockConsumption ||
            exception.Entries[0].State != EntityState.Added ||
            exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Errors
            .Cast<SqlError>()
            .Any(error =>
                error.Message.Contains(ConsumptionTableToken, StringComparison.Ordinal) &&
                (error.Number == 2601 &&
                    error.Message.Contains(IdempotencyIndexToken, StringComparison.Ordinal) ||
                 error.Number == 2627 &&
                    error.Message.Contains(IdempotencyConstraintToken, StringComparison.Ordinal)));
    }

    internal static bool IsDeadlock(Exception exception) =>
        FindSqlException(exception)?.Errors
            .Cast<SqlError>()
            .Any(error => error.Number == 1205) == true;

    private static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }
}
