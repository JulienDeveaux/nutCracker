using System.Net.WebSockets;
using System.Text;
using nutCracker.Models;

namespace nutCracker.Services;

public class WebsocketService
{
    public const int MaxPasswordLength = 4;
    // [a-zA-Z0-9]                  //min/ maj/ nb
    public static readonly char[] CharactersAvailable = {
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
    };
    
    private static int _totalSlavesCount = 0;
    
    private List<Slave> Slaves { get; }
    
    /// <summary>
    ///  null value = pending <br/>
    ///  empty value = not found <br/>
    ///  value = found
    /// </summary>
    private Dictionary<string, string> Md5Hashes { get; }

    public WebsocketService()
    {
        Slaves = new List<Slave>();
        Md5Hashes = new Dictionary<string, string>();
    }
    
    public async Task RegisterSlave(WebSocket socket)
    {
        var slave = new Slave
        {
            Id = _totalSlavesCount++,
            WebSocket = socket,
            Status = SlaveStatus.Ready
        };
        
        Slaves.Add(slave);
        
        var buffer = new byte[4 * 1024];

        WebSocketReceiveResult received;

        do
        {
            received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            var message = Encoding.UTF8.GetString(buffer, 0, received.Count);
            
            Console.WriteLine($"message received from slave {slave.Id}: '{message}'");

            var tab = message.Split(' ');
            
            switch (tab[0])
            {
                case "found" when tab.Length == 3:
                    Md5Hashes[tab[1]] = tab[2];
                    slave.Status = SlaveStatus.Ready;
                break;
                
                case "notFound" when tab.Length == 3:
                    Md5Hashes[tab[1]] = string.Empty;
                    slave.Status = SlaveStatus.Ready;
                break;
            }
        } 
        while (!received.CloseStatus.HasValue);

        slave.Status = SlaveStatus.Dead;

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

        Slaves.Remove(slave);
    }
    
    public async Task<string> Crack(string md5Hash)
    {
        var waitingSlaves = Slaves.Where(s => s.Status == SlaveStatus.Ready).ToArray();

        if (waitingSlaves.Length == 0)
        {
            return null;
        }
        
        var allAlphabets = DetermineAlphabets(waitingSlaves.Length);
        
        Md5Hashes[md5Hash] = null;

        for(var i = 0; i < waitingSlaves.Length; i++)
        {
            var slave = waitingSlaves[i];
            var alphabet = allAlphabets[i];
            
            slave.Status = SlaveStatus.Working;

            await slave.WebSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes($"search {md5Hash} {alphabet[..alphabet.IndexOf("|", StringComparison.Ordinal)]} {alphabet[(alphabet.IndexOf("|", StringComparison.Ordinal) + 1)..]}")),
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None);
        }
        
        while (string.IsNullOrWhiteSpace(Md5Hashes[md5Hash]) && waitingSlaves.Any(s => s.Status == SlaveStatus.Working))
        {
            await Task.Delay(100);
        }

        return Md5Hashes[md5Hash];
    }

    private string[] DetermineAlphabets(int nbSlaves)
    {
        var alphabets = new string[nbSlaves];
        
        var nb = MaxPasswordLength / nbSlaves;

        // todo split alphabet in nbSlaves parts
        for (var i = 0; i < nbSlaves; i++)
        {
            var nextNb = nb * (i + 1);

            alphabets[i] = $"{CharactersAvailable[nb * i]}|{(string.Join("", new char[MaxPasswordLength].Select(_ => CharactersAvailable[Math.Min(nextNb, CharactersAvailable.Length - 1)])))}";
        }
        
        return alphabets;
    }
}