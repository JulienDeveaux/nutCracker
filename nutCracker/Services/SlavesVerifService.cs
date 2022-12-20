namespace nutCracker.Services;

public class SlaveVerifService: BackgroundService
{
    private WebsocketService WebsocketService { get; }
    private DockerService DockerService { get; }
    
    public SlaveVerifService(WebsocketService websocketService, DockerService dockerService)
    {
        WebsocketService = websocketService;
        DockerService = dockerService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            WebsocketService.VerifSlaves();

            await Task.Delay(1000, stoppingToken);
        }
    }
}