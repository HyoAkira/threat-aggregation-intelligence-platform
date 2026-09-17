using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// EF Core write model for admin provider management. Provider configuration is validated against
/// the domain invariants before persisting, and a provider that still owns fetch history or IOC
/// records is never deleted implicitly.
/// </summary>
public sealed class AdminProviderRepository(IDbContextFactory<ThreatIntelligenceDbContext> contextFactory)
    : IAdminProviderRepository
{
    public async Task<ProviderWriteResult> CreateAsync(ProviderWriteRequest request, CancellationToken cancellationToken)
    {
        if (!TryMapConfiguration(request, out var defaultMode, out var supportedModes))
        {
            return new ProviderWriteResult(ProviderWriteStatus.Invalid, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.Providers.AnyAsync(provider => provider.Name == request.Name, cancellationToken))
        {
            return new ProviderWriteResult(ProviderWriteStatus.DuplicateName, null);
        }

        Provider provider;
        try
        {
            provider = new Provider(
                request.Name,
                request.BaseUrl,
                request.IntervalMinutes,
                request.IsEnabled,
                defaultMode: defaultMode,
                supportedModes: supportedModes,
                recentCooldownMinutes: request.RecentCooldownMinutes,
                fullCooldownMinutes: request.FullCooldownMinutes,
                minimumIntervalMinutes: request.MinimumIntervalMinutes,
                requiresApiKey: request.RequiresApiKey);
        }
        catch (ArgumentException)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Invalid, null);
        }

        context.Providers.Add(provider);
        await context.SaveChangesAsync(cancellationToken);
        return new ProviderWriteResult(ProviderWriteStatus.Success, AdminProviderProjection.Project(provider));
    }

    public async Task<ProviderWriteResult> UpdateAsync(
        int providerId,
        ProviderWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryMapConfiguration(request, out var defaultMode, out var supportedModes))
        {
            return new ProviderWriteResult(ProviderWriteStatus.Invalid, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await context.Providers.SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.NotFound, null);
        }

        if (await context.Providers.AnyAsync(
                item => item.Id != providerId && item.Name == request.Name, cancellationToken))
        {
            return new ProviderWriteResult(ProviderWriteStatus.DuplicateName, null);
        }

        try
        {
            provider.Configure(
                defaultMode,
                supportedModes,
                request.RecentCooldownMinutes,
                request.FullCooldownMinutes,
                request.MinimumIntervalMinutes);
            provider.SetEnabled(request.IsEnabled);
            provider.SetIntervalMinutes(request.IntervalMinutes);
            provider.SetRequiresApiKey(request.RequiresApiKey);
        }
        catch (ArgumentException)
        {
            return new ProviderWriteResult(ProviderWriteStatus.Invalid, null);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new ProviderWriteResult(ProviderWriteStatus.Success, AdminProviderProjection.Project(provider));
    }

    public async Task<ProviderWriteResult> DeleteAsync(int providerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await context.Providers.SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return new ProviderWriteResult(ProviderWriteStatus.NotFound, null);
        }

        var hasHistory = await context.FetchHistory.AnyAsync(history => history.ProviderId == providerId, cancellationToken);
        var hasIocs = await context.Iocs.AnyAsync(ioc => ioc.ProviderId == providerId, cancellationToken);
        if (hasHistory || hasIocs)
        {
            return new ProviderWriteResult(ProviderWriteStatus.InUse, null);
        }

        context.Providers.Remove(provider);
        await context.SaveChangesAsync(cancellationToken);
        return new ProviderWriteResult(ProviderWriteStatus.Success, null);
    }

    private static bool TryMapConfiguration(
        ProviderWriteRequest request,
        out FetchMode defaultMode,
        out ProviderFetchModes supportedModes)
    {
        defaultMode = default;
        supportedModes = ProviderFetchModes.None;

        var parsedDefault = FetchTokens.ParseMode(request.DefaultMode);
        if (parsedDefault is null || request.SupportedModes is null || request.SupportedModes.Count == 0)
        {
            return false;
        }

        var modes = ProviderFetchModes.None;
        foreach (var token in request.SupportedModes)
        {
            var parsed = FetchTokens.ParseMode(token);
            if (parsed is null)
            {
                return false;
            }

            modes |= parsed.Value.ToProviderMode();
        }

        if ((modes & parsedDefault.Value.ToProviderMode()) == ProviderFetchModes.None)
        {
            return false;
        }

        if (request.IntervalMinutes < request.MinimumIntervalMinutes)
        {
            return false;
        }

        defaultMode = parsedDefault.Value;
        supportedModes = modes;
        return true;
    }
}
