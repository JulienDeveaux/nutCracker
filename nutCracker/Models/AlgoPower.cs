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
    Haaaaaa = 128,
    IamDead = 245 // max value before infinite waiting = 249
}

public static class AlgoPowerExtension
{
    public static byte Value(this AlgoPower power) => (byte) power;
}