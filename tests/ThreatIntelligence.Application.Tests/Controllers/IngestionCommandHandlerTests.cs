using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;
using ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;
using ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;
using ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

namespace ThreatIntelligence.Application.Tests.Controllers;

public class IngestionCommandHandlerTests
{
    [Fact]
    public async Task ThreatFoxHandlersDelegateToTheExpectedServiceOperation()
    {
        var service = new ThreatFoxService();
        var download = new DownloadThreatFoxDatasetCommandHandler(service);
        var recent = new GetRecentThreatFoxIoCsCommandHandler(service);

        var downloadResponse = await download.Handle(new DownloadThreatFoxDatasetCommand(), CancellationToken.None);
        var recentResponse = await recent.Handle(new GetRecentThreatFoxIoCsCommand(), CancellationToken.None);

        Assert.True(service.DownloadCalled);
        Assert.True(service.RecentCalled);
        Assert.Equal("download", downloadResponse.Message);
        Assert.Equal("recent", recentResponse.Message);
    }

    [Fact]
    public async Task PhishTankHandlerDelegatesToTheDownloadServiceOperation()
    {
        var service = new PhishTankService();
        var handler = new DownloadPhishTankDatasetCommandHandler(service);

        var response = await handler.Handle(new DownloadPhishTankDatasetCommand(), CancellationToken.None);

        Assert.True(service.DownloadCalled);
        Assert.Equal("download", response.Message);
    }

    private sealed class ThreatFoxService : IThreatFoxApiService
    {
        public bool DownloadCalled { get; private set; }
        public bool RecentCalled { get; private set; }

        public Task<DownloadDatasetResponse> DownloadDatasetAsync(CancellationToken cancellationToken = default)
        {
            DownloadCalled = true;
            return Task.FromResult(new DownloadDatasetResponse(1, "download", DateTime.UtcNow));
        }

        public Task<GetRecentIoCsResponse> RecentIoCsAsync(CancellationToken cancellationToken = default)
        {
            RecentCalled = true;
            return Task.FromResult(new GetRecentIoCsResponse(1, "recent", DateTime.UtcNow));
        }
    }

    private sealed class PhishTankService : IPhishTankApiService
    {
        public bool DownloadCalled { get; private set; }

        public Task<DownloadDatasetResponse> DownloadDatasetAsync(CancellationToken cancellationToken = default)
        {
            DownloadCalled = true;
            return Task.FromResult(new DownloadDatasetResponse(1, "download", DateTime.UtcNow));
        }
    }
}
