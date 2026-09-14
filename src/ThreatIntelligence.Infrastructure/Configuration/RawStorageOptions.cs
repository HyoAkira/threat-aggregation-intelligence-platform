namespace ThreatIntelligence.Infrastructure.Configuration;

/// <summary>
/// Root directory under which provider feeds are persisted as
/// <c>{BasePath}/{provider}/{recents|fulls}/</c>. Relative paths resolve against the
/// process working directory.
/// </summary>
public sealed record RawStorageOptions(string BasePath);
