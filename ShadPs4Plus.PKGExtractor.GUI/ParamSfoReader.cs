using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ShadPs4Plus.PKGExtractor.GUI;

public sealed class ParamSfoData
{
    public string Title { get; init; } = "";
    public string TitleId { get; init; } = "";
    public string AppVersion { get; init; } = "";
    public string Category { get; init; } = "";
}

public static class ParamSfoReader
{
    public static ParamSfoData Read(string path)
    {
        byte[] data = File.ReadAllBytes(path);

        // Cabecera PSF: 00 50 53 46
        if (data.Length < 20 ||
            data[0] != 0x00 ||
            data[1] != 0x50 ||
            data[2] != 0x53 ||
            data[3] != 0x46)
        {
            throw new InvalidDataException(
                "El archivo no tiene una cabecera PARAM.SFO válida.");
        }

        uint keyTableOffset = ReadUInt32(data, 8);
        uint dataTableOffset = ReadUInt32(data, 12);
        uint entryCount = ReadUInt32(data, 16);

        if (entryCount > 10000 ||
            20L + entryCount * 16L > data.Length ||
            keyTableOffset >= data.Length ||
            dataTableOffset >= data.Length)
        {
            throw new InvalidDataException(
                "La estructura interna del PARAM.SFO no es válida.");
        }

        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < (int)entryCount; i++)
        {
            int entryOffset = checked(20 + i * 16);

            ushort keyOffset = ReadUInt16(data, entryOffset);
            ushort format = ReadUInt16(data, entryOffset + 2);
            uint valueLength = ReadUInt32(data, entryOffset + 4);
            uint valueMaxLength = ReadUInt32(data, entryOffset + 8);
            uint valueOffset = ReadUInt32(data, entryOffset + 12);

            long keyPosition = (long)keyTableOffset + keyOffset;
            long valuePosition = (long)dataTableOffset + valueOffset;

            if (keyPosition < 0 || keyPosition >= data.Length ||
                valuePosition < 0 || valuePosition > data.Length ||
                valueLength > data.Length - valuePosition ||
                valueLength > valueMaxLength)
            {
                throw new InvalidDataException(
                    $"La entrada {i} de PARAM.SFO está fuera de los límites.");
            }

            int keyEnd = Array.IndexOf(
                data, (byte)0, (int)keyPosition,
                data.Length - (int)keyPosition);

            if (keyEnd < 0)
                throw new InvalidDataException(
                    "No se encontró el final de una clave SFO.");

            string key = Encoding.UTF8.GetString(
                data, (int)keyPosition, keyEnd - (int)keyPosition);

            int length = checked((int)valueLength);
            int position = checked((int)valuePosition);

            string value;

            if (format == 0x0204)
            {
                value = Encoding.UTF8.GetString(
                    data, position, length).TrimEnd('\0').Trim();
            }
            else if (format == 0x0404 && length >= 4)
            {
                value = ReadUInt32(data, position).ToString();
            }
            else
            {
                // Conserva otros formatos como hexadecimal.
                value = Convert.ToHexString(
                    data.AsSpan(position, length));
            }

            values[key] = value;
        }

        return new ParamSfoData
        {
            Title = Get(values, "TITLE"),
            TitleId = Get(values, "TITLE_ID"),
            AppVersion = Get(values, "APP_VER"),
            Category = Get(values, "CATEGORY")
        };
    }

    private static string Get(
        Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? value)
            ? value
            : "";
    }

    private static ushort ReadUInt16(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(
            data.AsSpan(offset, 2));
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(
            data.AsSpan(offset, 4));
    }
}