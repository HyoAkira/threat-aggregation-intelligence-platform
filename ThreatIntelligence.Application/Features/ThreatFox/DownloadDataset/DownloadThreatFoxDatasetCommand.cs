using MediatR;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;

public record DownloadThreatFoxDatasetCommand : IRequest<DownloadDatasetResponse>;