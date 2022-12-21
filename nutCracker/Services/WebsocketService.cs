using System.Net.WebSockets;
using System.Text;
using nutCracker.Models;

namespace nutCracker.Services;

public class WebsocketService
{
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static int _totalSlavesCount = 0;
    private static readonly Semaphore ToTalCountSemaphore = new(1, 1);

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

    public int GetNbSlaves(SlaveStatus? status = null)
    {
        return Slaves.Count(slave => status is null || slave.Status == status);
    }

    public async Task RegisterSlave(WebSocket socket)
    {
        ToTalCountSemaphore.WaitOne();
        var slave = new Slave
        {
            Id = _totalSlavesCount++,
            WebSocket = socket,
            Status = SlaveStatus.Ready
        };
        ToTalCountSemaphore.Release();

        Slaves.Add(slave);

        var buffer = new byte[4 * 1024];

        WebSocketReceiveResult received;

        do
        {
            received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            var message = Encoding.UTF8.GetString(buffer, 0, received.Count);

            Console.WriteLine($"message received from slave {slave.Id}: '{message}'");

            var tab = message.Split(' ');

            if (slave.HashInWorking == null)
            {
                if(!message.StartsWith("slave"))
                    Console.WriteLine($"no hash in working for slave {slave.Id}");
                continue;
            }

            // ne pas centralisé les résultats (ex: slave.* = * en dehors des ifs) car rien ne dit qu'un slave 
            // ne renverras pas un autre type de message (code exterieur au projet donc inconnu)
            if (tab.Length == 3 && tab[0].StartsWith("found"))
            {
                Md5Hashes[slave.HashInWorking] = tab[2];
                
                slave.Status = SlaveStatus.Ready;

                await StopOthersSlaves(slave.HashInWorking);

                slave.LastWork = DateTime.Now;
                slave.HashInWorking = null;
            }
            else if (message.StartsWith("notfound"))
            {
                Md5Hashes[slave.HashInWorking] ??= string.Empty;
                    
                slave.Status = SlaveStatus.Ready;
                slave.LastWork = DateTime.Now;
                slave.HashInWorking = null;
            }
            
        } while (!received.CloseStatus.HasValue);

        slave.Status = SlaveStatus.Dead;

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

        Slaves.Remove(slave);
    }
    
    private async Task StopOthersSlaves(string md5Hash)
    {
        var tasks = Slaves
            .Where(slave => slave.HashInWorking == md5Hash && slave.Status == SlaveStatus.Working)
            .Select(slave => Task.Run(async () =>
            {
                await slave.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("stop")), WebSocketMessageType.Text, true, CancellationToken.None);

                Console.WriteLine($"slave {slave.Id} stopped");
            }))
            .ToList();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="md5Hash">Hash with max size is = <see cref="MaxPasswordLength"/></param>
    /// <param name="maxPasswordLength"></param>
    /// <returns>
    ///  null value = pending <br/>
    ///  empty value = not found <br/>
    ///  value = found
    /// </returns>
    public async Task<string> Crack(string md5Hash, int maxPasswordLength)
    {
        var waitingSlaves = Slaves.Where(s => s.Status == SlaveStatus.Ready && s.WebSocket.State == WebSocketState.Open).ToArray();

        if (waitingSlaves.Length == 0)
            return null;
        Console.WriteLine("determine alphabets");
        var allAlphabets = DetermineAlphabets(waitingSlaves.Length, maxPasswordLength);

        Console.WriteLine(string.Join("\n", allAlphabets));
        
        Md5Hashes[md5Hash] = null;

        for (var i = 0; i < waitingSlaves.Length; i++)
        {
            var slave = waitingSlaves[i];
            var alphabet = allAlphabets[i];

            slave.Status = SlaveStatus.Working;

            var cmd = $"search {md5Hash} {alphabet[..alphabet.IndexOf("|", StringComparison.Ordinal)]} {alphabet[(alphabet.IndexOf("|", StringComparison.Ordinal) + 1)..]}";

            Console.WriteLine($"cmd send to slave {slave.Id}: '{cmd}'");
            
            await slave.WebSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(cmd)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            slave.HashInWorking = md5Hash;
        }

        while (Md5Hashes[md5Hash] is null || Md5Hashes[md5Hash].Length == 0 && waitingSlaves.Any(s => s.Status == SlaveStatus.Working))
        {
            await Task.Delay(100);
        }
        
        Console.WriteLine("crack ended");

        return Md5Hashes[md5Hash];
    }

    private static string[] DetermineAlphabets(int nbSlaves, int maxPasswordLength)
    {
        var alphabetsToReturn = new string[nbSlaves];
        long alphabetSize;
        
        alphabetsToReturn[0] = "a";
        
        if (maxPasswordLength == 1)
        {
            alphabetSize = (long)Math.Round(Math.Pow(62, maxPasswordLength));
        }
        else
        {
            alphabetSize = (long)Math.Round(Math.Pow(62, maxPasswordLength + 1));
        }

        var splitPosition = (int)Math.Round((1.0 * alphabetSize / nbSlaves));
        var splitPositions = new int[nbSlaves - 1];
        
        var it = splitPosition;
        for (var i = 0; i < splitPositions.Length; i++)
        {
            splitPositions[i] = it;
            it += splitPosition;
        }

        var previousEnd = "a";
        for (var i = 0; i < splitPositions.Length; i++)
        {
            var transformIn64Format = new int[maxPasswordLength];
            
            for (var j = 0; j < maxPasswordLength; j++)
            {
                var wordPos = (1.0 * splitPositions[i] / (62 * maxPasswordLength)) * 62;
                var letterPos = (int) Math.Round(((wordPos / maxPasswordLength) * (j + 1)));
                var toLetter = letterPos % 62;
                
                transformIn64Format[j] = toLetter;
            }

            var nextWord = transformIn64Format.Aggregate("", (current, letter) => current + Alphabet[letter]);

            alphabetsToReturn[i] = previousEnd + "|" + nextWord;

            var letterArray = new int[maxPasswordLength];
            for (var j = 0; j < transformIn64Format.Length; j++)
            {
                var num = transformIn64Format[j];
                if (num >= 62)
                    num = 61;

                letterArray[j] = num;

                if (j != transformIn64Format.Length - 1) 
                    continue;
                
                if (letterArray[j] == 61)
                {
                    letterArray[j] = 0;
                    var depth = 1;
                    
                    while (depth < maxPasswordLength)
                    {
                        if (letterArray[j - depth] < 61)
                        {
                            letterArray[j - depth] += 1;
                            depth = maxPasswordLength;
                        }
                        else
                        {
                            letterArray[j - depth] = 0;
                            depth++;
                        }
                    }
                }
                else
                {
                    letterArray[j] += 1;
                }
            }

            previousEnd = letterArray.Aggregate("", (current, letter) => current + Alphabet[letter]);
        }

        var howMany9 = "";
        for (var i = 0; i < maxPasswordLength; i++)
            howMany9 += "9";

        alphabetsToReturn[nbSlaves - 1] = previousEnd + "|" + howMany9;
        
        return alphabetsToReturn;
    }

    public Dictionary<string, string>[] SlavesStatus()
    {
        return Slaves.Select(s => new Dictionary<string, string>
        {
            {"id", s.Id.ToString()},
            {"status", s.Status.ToString()},
            {"webSocketState", s.WebSocket.State.ToString()},
            {"lastWork", s.LastWork?.ToString("dd/MM/yyyy HH:mm:ss")},
            {"hashInWorking", s.HashInWorking}
        }).ToArray();
    }

    internal void VerifSlaves()
    {
        foreach (var slave in Slaves.ToList())
        {
            if (slave.WebSocket.State != WebSocketState.Open)
            {
                slave.Status = SlaveStatus.Dead;
                slave.HashInWorking = null;
            }
            else if (slave.Status != SlaveStatus.Working)
            {
                slave.Status = SlaveStatus.Ready;
                slave.HashInWorking = null;
            }
        }
    }

    internal int[] GetSlavesIdsToDown(int maxNbMinute = 5)
    {
        return Slaves.ToList()
            .Where(slave => slave.Status == SlaveStatus.Ready && slave.LastWork != null && DateTime.Now - slave.LastWork > TimeSpan.FromMinutes(maxNbMinute))
            .Select(s => s.Id)
            .ToArray();
    }

    internal async Task DownSlaves(int[] ids)
    {
        foreach (var id in ids)
        {
            var slave = Slaves.FirstOrDefault(s => s.Id == id);
            if (slave is null)
                continue;
            
            await slave.WebSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes("exit")),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            Slaves.Remove(slave);
        }
    }
}