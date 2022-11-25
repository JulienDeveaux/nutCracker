using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using nutCracker.Models;
using nutCracker.Services;

namespace nutCracker.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    
    private DockerService DockerService { get; }

    public HomeController(ILogger<HomeController> logger, DockerService dockerService)
    {
        _logger = logger;
        DockerService = dockerService;
    }

    public async Task<IActionResult> Index()
    {
        await DockerService.InitService();
        
        return View();
    }
    
    [Route("/ws")]
    public async Task WebSocket()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            var buffer = new byte[4 * 1024];

            WebSocketReceiveResult received = null;

            do
            {
                received = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                Console.WriteLine(Encoding.ASCII.GetString(buffer));
            } 
            while (!received.CloseStatus.HasValue);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}