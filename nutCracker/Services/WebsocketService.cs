using System.Net.WebSockets;
using System.Text;
using nutCracker.Models;

namespace nutCracker.Services;

public class WebsocketService
{
    public const int MaxPasswordLength = 4;

    public static string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

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
        } while (!received.CloseStatus.HasValue);

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

        for (var i = 0; i < waitingSlaves.Length; i++)
        {
            var slave = waitingSlaves[i];
            var alphabet = allAlphabets[i];

            slave.Status = SlaveStatus.Working;

            await slave.WebSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(
                    $"search {md5Hash} {alphabet[..alphabet.IndexOf("|", StringComparison.Ordinal)]} {alphabet[(alphabet.IndexOf("|", StringComparison.Ordinal) + 1)..]}")),
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
        var alphabetsToReturn = new string[nbSlaves];
        alphabetsToReturn[0] = "a";
        long alphabetSize;
        if (MaxPasswordLength == 1)
        {
            alphabetSize = (long)Math.Round(Math.Pow(62, MaxPasswordLength));
        }
        else
        {
            alphabetSize = (long)Math.Round(Math.Pow(62, MaxPasswordLength + 1));
        }

        var split_position = (int)Math.Round((1.0 * alphabetSize / nbSlaves));
        var split_positions = new int[nbSlaves - 1];
        var it = split_position;
        for (var i = 0; i < split_positions.Length; i++)
        {
            split_positions[i] = it;
            it += split_position;
        }

        var previousEnd = "a";
        for (var i = 0; i < split_positions.Length; i++)
        {
            var transformIn64Format = new int[MaxPasswordLength];
            for (var j = 0; j < MaxPasswordLength; j++)
            {
                var wordPos = (1.0 * split_positions[i] / (62 * MaxPasswordLength)) * 62;
                var letterPos = (int)Math.Round(((wordPos / MaxPasswordLength) * (j + 1)));
                var toLetter = letterPos % 62;
                transformIn64Format[j] = toLetter;
            }

            var nextWord = "";
            foreach (var letter in transformIn64Format)
            {
                nextWord += alphabet[letter];
            }

            alphabetsToReturn[i] = previousEnd + "|" + nextWord;
            previousEnd = "";

            var letterArray = new int[MaxPasswordLength];
            for (var j = 0; j < transformIn64Format.Length; j++)
            {
                var num = transformIn64Format[j];
                if (num >= 62)
                {
                    num = 61;
                }

                letterArray[j] = num;

                if (j == transformIn64Format.Length - 1)
                {
                    if (letterArray[j] == 61)
                    {
                        letterArray[j] = 0;
                        var depth = 1;
                        while (depth < MaxPasswordLength)
                        {
                            if (letterArray[j - depth] < 61)
                            {
                                letterArray[j - depth] += 1;
                                depth = MaxPasswordLength;
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
            }

            foreach (var letter in letterArray)
            {
                previousEnd += alphabet[letter];
            }
        }

        var howMany9 = "";
        for (var i = 0; i < MaxPasswordLength; i++)
        {
            howMany9 += "9";
        }

        alphabetsToReturn[nbSlaves - 1] = previousEnd + "|" + howMany9;
        return alphabetsToReturn;
    }
}