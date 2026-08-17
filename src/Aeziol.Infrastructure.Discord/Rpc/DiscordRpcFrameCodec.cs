using System.Buffers.Binary;

namespace Aeziol.Infrastructure.Discord.Rpc;

internal static class DiscordRpcFrameCodec
{
    internal const int HeaderLength = 8;
    internal const int MaximumPayloadLength = 1024 * 1024;

    public static byte[] Encode(DiscordRpcOpcode opcode, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Discord RPC payload exceeds the safety limit.");
        }

        var frame = new byte[HeaderLength + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), (int)opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderLength));
        return frame;
    }

    public static async Task<DiscordRpcFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);

        var opcodeValue = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (!Enum.IsDefined((DiscordRpcOpcode)opcodeValue))
        {
            throw new InvalidDataException($"Unknown Discord RPC opcode: {opcodeValue}.");
        }

        if (payloadLength is < 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException("Discord RPC payload length is outside the accepted range.");
        }

        var payload = new byte[payloadLength];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return new DiscordRpcFrame((DiscordRpcOpcode)opcodeValue, payload);
    }
}
