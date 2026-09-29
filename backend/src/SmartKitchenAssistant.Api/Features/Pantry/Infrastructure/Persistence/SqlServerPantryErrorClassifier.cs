using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal static class SqlServerPantryErrorClassifier
{
    private const string PantryTableToken =
        "object 'pantry.UserPantryItems'";
    private const string UserIngredientUniqueIndexToken =
        "unique index '" +
        UserPantryItemConfiguration.UserIngredientUniqueIndexName +
        "'";
    private const string UserIngredientUniqueConstraintToken =
        "constraint '" +
        UserPantryItemConfiguration.UserIngredientUniqueIndexName +
        "'";

    internal static bool IsDuplicateUserIngredient(DbUpdateException exception)
    {
        if (exception.Entries.Count != 1 ||
            exception.Entries[0].Entity is not UserPantryItem ||
            exception.Entries[0].State != EntityState.Added ||
            exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Errors
            .Cast<SqlError>()
            .Any(error => IsTargetUniqueViolation(error.Number, error.Message));
    }

    private static bool IsTargetUniqueViolation(int number, string message) =>
        message.Contains(PantryTableToken, StringComparison.Ordinal) &&
        (number == 2601 &&
            message.Contains(UserIngredientUniqueIndexToken, StringComparison.Ordinal) ||
         number == 2627 &&
            message.Contains(UserIngredientUniqueConstraintToken, StringComparison.Ordinal));
}
