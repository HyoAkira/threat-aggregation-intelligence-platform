using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ThreatIntelligence.Infrastructure.Persistence;

/// <summary>
/// Forces persisted column names to lower-camel case (e.g. <c>iocValueHash</c>) as required by
/// the data contract. Explicitly configured column names are left untouched.
/// </summary>
internal sealed class CamelCaseColumnConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.GetColumnNameConfigurationSource() is null)
                {
                    property.SetColumnName(ToCamelCase(property.GetDefaultColumnName() ?? property.Name));
                }
            }
        }
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}