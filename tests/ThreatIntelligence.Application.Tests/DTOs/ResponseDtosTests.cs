using System.Reflection;
using System.Runtime.CompilerServices;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Tests.DTOs;

public class DownloadDatasetResponseTests
{
    [Fact]
    public void Constructor_WithCountMessageAndTimestamp_SetsAllProperties()
    {
        var timestamp = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        var response = new DownloadDatasetResponse(42, "Download completed", timestamp);

        Assert.Equal(42, response.RecordsProcessed);
        Assert.Equal("Download completed", response.Message);
        Assert.Equal(timestamp, response.FetchedAtUtc);
    }

    [Fact]
    public void DTO_HasNoPublicSetters()
    {
        foreach (var property in typeof(DownloadDatasetResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(DtoImmutability.IsInitOnly(property), $"{property.Name} must not be mutable after construction");
        }
    }

    [Fact]
    public void TwoDTOs_WithSameValues_AreEqual()
    {
        var timestamp = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var left = new DownloadDatasetResponse(42, "Download completed", timestamp);
        var right = new DownloadDatasetResponse(42, "Download completed", timestamp);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}

public class GetRecentIoCsResponseTests
{
    [Fact]
    public void Constructor_WithCountMessageAndTimestamp_SetsAllProperties()
    {
        var timestamp = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        var response = new GetRecentIoCsResponse(7, "Recent IOCs fetched", timestamp);

        Assert.Equal(7, response.RecordsProcessed);
        Assert.Equal("Recent IOCs fetched", response.Message);
        Assert.Equal(timestamp, response.FetchedAtUtc);
    }

    [Fact]
    public void DTO_HasNoPublicSetters()
    {
        foreach (var property in typeof(GetRecentIoCsResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(DtoImmutability.IsInitOnly(property), $"{property.Name} must not be mutable after construction");
        }
    }

    [Fact]
    public void TwoDTOs_WithSameValues_AreEqual()
    {
        var timestamp = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var left = new GetRecentIoCsResponse(7, "Recent IOCs fetched", timestamp);
        var right = new GetRecentIoCsResponse(7, "Recent IOCs fetched", timestamp);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}

internal static class DtoImmutability
{
    public static bool IsInitOnly(PropertyInfo property)
    {
        var setter = property.SetMethod;
        if (setter is null)
        {
            return true;
        }

        return setter.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(IsExternalInit));
    }
}