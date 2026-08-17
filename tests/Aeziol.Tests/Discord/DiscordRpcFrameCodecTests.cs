using System.Buffers.Binary;
using System.Text;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Tests.Discord;

public sealed class DiscordRpcFrameCodecTests
{
    [Fact]
    public async Task EncodeAndReadAsync_RoundTripsFrame()
    {
        var payload = Encoding.UTF8.GetBytes("{\"cmd\":\"DISPATCH\"}");
        var encoded = DiscordRpcFrameCodec.Encode(DiscordRpcOpcode.Frame, payload);

        await using var stream = new MemoryStream(encoded);
        var decoded = await DiscordRpcFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(DiscordRpcOpcode.Frame, decoded.Opcode);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public async Task ReadAsync_RejectsPayloadLargerThanSafetyLimit()
    {
        var header = new byte[DiscordRpcFrameCodec.HeaderLength];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), (int)DiscordRpcOpcode.Frame);
        BinaryPrimitives.WriteInt32LittleEndian(
            header.AsSpan(4, 4),
            DiscordRpcFrameCodec.MaximumPayloadLength + 1);

        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => DiscordRpcFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_RejectsUnknownOpcode()
    {
        var header = new byte[DiscordRpcFrameCodec.HeaderLength];
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0, 4), 99);

        await using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => DiscordRpcFrameCodec.ReadAsync(stream, TestContext.Current.CancellationToken));
    }
}
