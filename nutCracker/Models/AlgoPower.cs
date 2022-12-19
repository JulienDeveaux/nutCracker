namespace nutCracker.Models;

public enum AlgoPower: byte
{
    Classic = 1,
    More = 2,
    Fast = 4,
    Fastest = 8,
    FastestPlus = 16,
    Extreme = 32,
    ExtremePlus = 64,
    Haaaaaaaaaaaaaaaaaaaaaaaaa = 128,
    IamDead = byte.MaxValue
}

public static class AlgoPowerExtension
{
    public static byte Value(this AlgoPower power) => (byte) power;
}