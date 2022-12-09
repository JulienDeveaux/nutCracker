using System.Net.WebSockets;

namespace nutCracker.Models;

public class Slave
{
    public int Id { get; set; }
    public WebSocket WebSocket { get; set; }
    public SlaveStatus Status { get; set; }
}