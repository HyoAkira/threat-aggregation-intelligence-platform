namespace ThreatIntelligence.Domain.Enums;

public enum FetchStatus : byte
{
    Queued = 0,
    Running = 1,
    Success = 2,
    Failed = 3
}