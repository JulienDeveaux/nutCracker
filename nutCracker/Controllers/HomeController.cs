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
    private WebsocketService WebsocketService { get; }

    public HomeController(
        ILogger<HomeController> logger, 
        DockerService dockerService,
        WebsocketService websocketService)
    {
        _logger = logger;
        DockerService = dockerService;
        WebsocketService = websocketService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await DockerService.InitService();
        
        return View();
    }
    
    [HttpPost]
    public async Task<IActionResult> Index(string hash)
    {
        if (WebsocketService.GetNbSlaves(SlaveStatus.Ready) == 0)
        {
            int nbSlaves = WebsocketService.GetNbSlaves();
            await DockerService.AddNewSlave();
            
            Console.WriteLine("awaiting new slave");
            
            while(nbSlaves == WebsocketService.GetNbSlaves())
            {
                await Task.Delay(1000);
            }
            
            Console.WriteLine("new slave available");
        }
        
        var mdp = await WebsocketService.Crack(hash);
        
        if(mdp == null)
            return StatusCode(500, "Une erreur est survenue");
        
        if(string.IsNullOrWhiteSpace(mdp))
            return NotFound("Le hash n'a pas été trouvé");

        return Ok(mdp);
    }
    
    [Route("/ws")]
    public async Task WebSocket()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

            await WebsocketService.RegisterSlave(webSocket);
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