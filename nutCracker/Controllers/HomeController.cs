using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using nutCracker.Database;
using nutCracker.Models;
using nutCracker.Services;

namespace nutCracker.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly NutCrackerContext _database;
    
    private DockerService DockerService { get; }
    private WebsocketService WebsocketService { get; }

    public HomeController(
        ILogger<HomeController> logger, 
        DockerService dockerService,
        WebsocketService websocketService,
        NutCrackerContext database)
    {
        _logger = logger;
        DockerService = dockerService;
        WebsocketService = websocketService;
        _database = database;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await DockerService.InitService();
        
        return View();
    }

    [HttpGet("slaves")]
    public IActionResult Slaves()
    {
        return View();
    }
    
    [HttpGet("hashs")]
    public IActionResult Hashs()
    {
        ViewData["hashs"] = _database.HashResults.ToArray();
        
        return View();
    }
    
    [HttpGet("authors")]
    public IActionResult Authors()
    {
        return View();
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