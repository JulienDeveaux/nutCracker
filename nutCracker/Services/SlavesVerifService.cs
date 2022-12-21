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

            var slaveIdsToDown = WebsocketService.GetSlavesIdsToDown(1);

            if (slaveIdsToDown.Length > 0)
            {
                await DockerService.RemoveSlaves(slaveIdsToDown.Length);
                await WebsocketService.DownSlaves(slaveIdsToDown);   
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}