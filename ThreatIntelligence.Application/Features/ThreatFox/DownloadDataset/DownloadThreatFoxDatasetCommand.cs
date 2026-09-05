using MediatR;

namespace ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;

public record DownloadThreatFoxDatasetCommand : IRequest<int>;
