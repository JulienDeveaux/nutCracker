using System.Net.WebSockets;
using System.Text;
using nutCracker.Models;

namespace nutCracker.Services;

public class WebsocketService
{
    public const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

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

    public int GetNbSlaves(SlaveStatus? status = null)
    {
        return Slaves.Count(slave => status is null || slave.Status == status);
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
        } while (!received.CloseStatus.HasValue);

        slave.Status = SlaveStatus.Dead;

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

        Slaves.Remove(slave);
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
        }

        while (string.IsNullOrWhiteSpace(Md5Hashes[md5Hash]) && waitingSlaves.Any(s => s.Status == SlaveStatus.Working))
        {
            await Task.Delay(100);
        }

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
}