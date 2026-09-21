using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class IocUpsertTests
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] OriginalHash = Enumerable.Repeat((byte)7, 32).ToArray();
    private static readonly byte[] NewHash = Enumerable.Repeat((byte)9, 32).ToArray();

    [Fact]
    public void Existing_row_preserves_creation_and_first_seen()
    {
        var ioc = new IoC(
            1, "rec-1", "1.2.3.4", OriginalHash, IocType.Ipv4, Utc, Utc, Utc);

        ioc.ApplyProviderUpsert(UpsertFields("rec-1", "1.2.3.4"));

        Assert.Equal(Utc, ioc.CreatedAt);
        Assert.Equal(Utc, ioc.FirstSeenUtc);
    }

    [Fact]
    public void Upsert_refreshes_updatedAt_and_replaces_last_seen()
    {
        var ioc = new IoC(
            1, "rec-1", "1.2.3.4", OriginalHash, IocType.Ipv4, Utc, Utc, Utc,
            lastSeenUtc: null);
        var newNow = Utc.AddDays(1);
        var newLastSeen = Utc.AddDays(2);

        ioc.ApplyProviderUpsert(UpsertFields("rec-1", "1.2.3.4", updatedAt: newNow, lastSeen: newLastSeen));

        Assert.Equal(newNow, ioc.UpdatedAt);
        Assert.Equal(newLastSeen, ioc.LastSeenUtc);
    }

    [Fact]
    public void Upsert_replaces_provider_metadata()
    {
        var ioc = new IoC(
            1, "rec-1", "1.2.3.4", OriginalHash, IocType.Ipv4, Utc, Utc, Utc,
            isVerified: true, target: "Old Bank", phishTankOnline: true, phishTankDetails: "{old}");

        ioc.ApplyProviderUpsert(new IocUpsertFields(
            ProviderRecordId: "rec-1",
            IocValue: "1.2.3.4",
            IocValueHash: OriginalHash,
            IocType: IocType.Ipv4,
            Port: null,
            ProviderDetailsLink: null,
            UpdatedAtUtc: Utc,
            FirstSeenUtc: Utc,
            LastSeenUtc: null,
            Tags: Array.Empty<string>(),
            ThreatType: null,
            Malware: null,
            MalwareAlias: null,
            MalwarePrintable: null,
            IsCompromised: null,
            Reporter: null,
            Anonymous: null,
            IsVerified: false,
            Target: "New Bank",
            PhishTankDetails: "{new}",
            PhishTankOnline: null,
            UrlHost: null,
            ParentDomain: null,
            ThreatFoxConfidenceLevel: null));

        Assert.False(ioc.IsVerified);
        Assert.Equal("New Bank", ioc.Target);
        Assert.Equal("{new}", ioc.PhishTankDetails);
        Assert.Null(ioc.PhishTankOnline);
    }

    [Fact]
    public void Upsert_can_move_the_normalized_identity()
    {
        var ioc = new IoC(
            1, "rec-1", "1.2.3.4", OriginalHash, IocType.Ipv4, Utc, Utc, Utc);

        ioc.ApplyProviderUpsert(UpsertFields("rec-1", "1.2.3.5", hash: NewHash, updatedAt: Utc.AddHours(1)));

        Assert.Equal("1.2.3.5", ioc.IocValue);
        Assert.Equal(NewHash, ioc.IocValueHash);
        Assert.Equal(Utc.AddHours(1), ioc.UpdatedAt);
    }

    [Fact]
    public void Existing_row_replaces_details_link_on_upsert()
    {
        var ioc = new IoC(
            1, "rec-1", "1.2.3.4", OriginalHash, IocType.Ipv4, Utc, Utc, Utc,
            providerDetailsLink: "https://old.example/link");

        ioc.ApplyProviderUpsert(UpsertFields("rec-1", "1.2.3.4", detailsLink: "https://new.example/link"));

        Assert.Equal("https://new.example/link", ioc.ProviderDetailsLink);
    }

    private static IocUpsertFields UpsertFields(
        string recordId,
        string value,
        byte[]? hash = null,
        DateTime? updatedAt = null,
        DateTime? lastSeen = null,
        string? detailsLink = null) => new(
        ProviderRecordId: recordId,
        IocValue: value,
        IocValueHash: hash ?? OriginalHash,
        IocType: IocType.Ipv4,
        Port: null,
        ProviderDetailsLink: detailsLink,
        UpdatedAtUtc: updatedAt ?? Utc.AddHours(1),
        FirstSeenUtc: Utc,
        LastSeenUtc: lastSeen,
        Tags: Array.Empty<string>(),
        ThreatType: null,
        Malware: null,
        MalwareAlias: null,
        MalwarePrintable: null,
        IsCompromised: null,
        Reporter: null,
        Anonymous: null,
        IsVerified: null,
        Target: null,
        PhishTankDetails: null,
        PhishTankOnline: null,
        UrlHost: null,
        ParentDomain: null,
        ThreatFoxConfidenceLevel: null);
}