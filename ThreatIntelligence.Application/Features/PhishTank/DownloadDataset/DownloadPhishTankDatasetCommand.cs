using MediatR;

namespace ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;

public record DownloadPhishTankDatasetCommand : IRequest<int>;
