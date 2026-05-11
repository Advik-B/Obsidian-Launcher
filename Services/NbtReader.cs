using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Serilog;

namespace ObsidianLauncher.Services;

public class NbtReader
{
    private static readonly ILogger Logger = Log.ForContext<NbtReader>();

    private const byte TagEnd = 0;
    private const byte TagByte = 1;
    private const byte TagShort = 2;
    private const byte TagInt = 3;
    private const byte TagLong = 4;
    private const byte TagFloat = 5;
    private const byte TagDouble = 6;
    private const byte TagByteArray = 7;
    private const byte TagString = 8;
    private const byte TagList = 9;
    private const byte TagCompound = 10;
    private const byte TagIntArray = 11;
    private const byte TagLongArray = 12;

    public static Dictionary<string, object> ReadCompressed(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            using var gzip = new GZipStream(fs, CompressionMode.Decompress);
            using var ms = new MemoryStream();
            gzip.CopyTo(ms);
            ms.Position = 0;
            using var reader = new BinaryReader(ms);
            var result = new Dictionary<string, object>();
            ReadTag(reader, result, "");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to read NBT from {FilePath}", filePath);
            return new Dictionary<string, object>();
        }
    }

    private static void ReadTag(BinaryReader reader, Dictionary<string, object> result, string prefix)
    {
        var type = reader.ReadByte();
        if (type == TagEnd) return;
        var nameLength = ReadBigEndianInt16(reader);
        var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
        var path = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
        ReadPayload(type, reader, result, path);
    }

    private static void ReadPayload(byte type, BinaryReader reader, Dictionary<string, object> result, string path)
    {
        switch (type)
        {
            case TagByte: result[path] = reader.ReadByte(); break;
            case TagShort: result[path] = ReadBigEndianInt16(reader); break;
            case TagInt: result[path] = ReadBigEndianInt32(reader); break;
            case TagLong: result[path] = ReadBigEndianInt64(reader); break;
            case TagFloat: result[path] = ReadBigEndianFloat(reader); break;
            case TagDouble: result[path] = ReadBigEndianDouble(reader); break;
            case TagByteArray:
                var baLen = ReadBigEndianInt32(reader);
                reader.ReadBytes(baLen);
                break;
            case TagString:
                var strLen = ReadBigEndianInt16(reader);
                result[path] = Encoding.UTF8.GetString(reader.ReadBytes(strLen));
                break;
            case TagList:
                var elementType = reader.ReadByte();
                var listLen = ReadBigEndianInt32(reader);
                for (var i = 0; i < listLen; i++)
                    ReadPayload(elementType, reader, result, $"{path}[{i}]");
                break;
            case TagCompound:
                ReadCompoundPayload(reader, result, path);
                break;
            case TagIntArray:
                var iaLen = ReadBigEndianInt32(reader);
                for (var i = 0; i < iaLen; i++) ReadBigEndianInt32(reader);
                break;
            case TagLongArray:
                var laLen = ReadBigEndianInt32(reader);
                for (var i = 0; i < laLen; i++) ReadBigEndianInt64(reader);
                break;
        }
    }

    private static void ReadCompoundPayload(BinaryReader reader, Dictionary<string, object> result, string prefix)
    {
        while (true)
        {
            var type = reader.ReadByte();
            if (type == TagEnd) break;
            var nameLength = ReadBigEndianInt16(reader);
            var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
            var path = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
            ReadPayload(type, reader, result, path);
        }
    }

    private static short ReadBigEndianInt16(BinaryReader r)
    {
        var b = r.ReadBytes(2);
        return (short)((b[0] << 8) | b[1]);
    }

    private static int ReadBigEndianInt32(BinaryReader r)
    {
        var b = r.ReadBytes(4);
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    private static long ReadBigEndianInt64(BinaryReader r)
    {
        var b = r.ReadBytes(8);
        return ((long)b[0] << 56) | ((long)b[1] << 48) | ((long)b[2] << 40) | ((long)b[3] << 32)
             | ((long)b[4] << 24) | ((long)b[5] << 16) | ((long)b[6] << 8) | b[7];
    }

    private static float ReadBigEndianFloat(BinaryReader r)
    {
        var b = r.ReadBytes(4);
        Array.Reverse(b);
        return BitConverter.ToSingle(b, 0);
    }

    private static double ReadBigEndianDouble(BinaryReader r)
    {
        var b = r.ReadBytes(8);
        Array.Reverse(b);
        return BitConverter.ToDouble(b, 0);
    }

    public static (string? LevelName, string? GameMode, DateTime? LastPlayed) ExtractLevelInfo(Dictionary<string, object> nbtData)
    {
        string? levelName = null;
        string? gameMode = null;
        DateTime? lastPlayed = null;

        if (nbtData.TryGetValue("Data.LevelName", out var lnObj) && lnObj is string ln)
            levelName = ln;

        if (nbtData.TryGetValue("Data.GameType", out var gmObj))
        {
            var gm = Convert.ToInt32(gmObj);
            gameMode = gm switch { 0 => "Survival", 1 => "Creative", 2 => "Adventure", 3 => "Spectator", _ => null };
        }

        if (nbtData.TryGetValue("Data.LastPlayed", out var lpObj))
        {
            var ms = Convert.ToInt64(lpObj);
            lastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }

        return (levelName, gameMode, lastPlayed);
    }
}
