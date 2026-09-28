using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence.ValueConverters;

internal sealed class Utf16LittleEndianStringToBytesConverter()
    : ValueConverter<string, byte[]>(
        value => Encode(value),
        value => Decode(value),
        new ConverterMappingHints(size: MaximumByteLength))
{
    public const int MaximumByteLength = UserPantryItem.MaximumUserIdLength * sizeof(char);

    private static byte[] Encode(string value)
    {
        // Encode code units directly so no fallback, normalization, or BOM can alter the value.
        var bytes = new byte[checked(value.Length * sizeof(char))];

        for (var index = 0; index < value.Length; index++)
        {
            var codeUnit = value[index];
            bytes[index * 2] = (byte)codeUnit;
            bytes[(index * 2) + 1] = (byte)(codeUnit >> 8);
        }

        return bytes;
    }

    private static string Decode(byte[] value)
    {
        if (value.Length % sizeof(char) != 0)
        {
            throw new InvalidOperationException(
                "A UTF-16 little-endian value must contain an even number of bytes.");
        }

        var codeUnits = new char[value.Length / sizeof(char)];

        for (var index = 0; index < codeUnits.Length; index++)
        {
            codeUnits[index] = (char)(value[index * 2] | (value[(index * 2) + 1] << 8));
        }

        return new string(codeUnits);
    }
}
