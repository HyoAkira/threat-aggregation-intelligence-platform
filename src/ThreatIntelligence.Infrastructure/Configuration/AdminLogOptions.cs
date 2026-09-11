namespace ThreatIntelligence.Infrastructure.Configuration;

/// <summary>
/// Location and rolling-file prefix of the process's Serilog file sink. The Admin log tail only
/// reads files matching this prefix in this directory, so a caller can never request another path.
/// </summary>
public sealed record AdminLogOptions(string Directory, string FileNamePrefix);
