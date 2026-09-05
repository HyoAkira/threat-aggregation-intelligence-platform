using MediatR;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;

public record DownloadPhishTankDatasetCommand : IRequest<DownloadDatasetResponse>;