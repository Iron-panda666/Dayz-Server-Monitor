using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MagDeer.Core;

public sealed class A2sClient
{
    private static readonly byte[] Request = [255, 255, 255, 255, 0x54, .. Encoding.ASCII.GetBytes("Source Engine Query\0")];

    public async Task<QueryResult> QueryAsync(string ip, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var watch = Stopwatch.StartNew();
        try
        {
            var address = IPAddress.Parse(ip);
            using var udp = new UdpClient(address.AddressFamily);
            udp.Connect(new IPEndPoint(address, port));
            var request = Request;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                await udp.SendAsync(request, deadline.Token).ConfigureAwait(false);
                var packet = await ReceiveMessageAsync(udp, deadline.Token).ConfigureAwait(false);
                if (packet.Length < 5 || BinaryPrimitives.ReadInt32LittleEndian(packet) != -1)
                    throw new InvalidDataException("Chybí hlavička A2S odpovědi.");
                if (packet[4] == 0x41)
                {
                    if (packet.Length != 9) throw new InvalidDataException("Neplatná délka challenge.");
                    request = [.. Request, .. packet.AsSpan(5, 4).ToArray()];
                    continue;
                }
                var info = ParseInfo(packet);
                return new(DateTimeOffset.UtcNow, info, watch.Elapsed.TotalMilliseconds, QueryFailure.None, "A2S_INFO OK");
            }
            throw new InvalidDataException("Server opakovaně vrací challenge místo A2S_INFO.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Failed(QueryFailure.Timeout, $"UDP query neodpovědělo do {timeout.TotalSeconds:0.#} s."); }
        catch (UnsupportedPacketException ex) { return Failed(QueryFailure.UnsupportedCompression, ex.Message); }
        catch (InvalidDataException ex) { return Failed(QueryFailure.InvalidResponse, ex.Message); }
        catch (SocketException ex) { return Failed(QueryFailure.Network, $"UDP síťová chyba: {ex.SocketErrorCode}."); }
        catch (FormatException) { return Failed(QueryFailure.Network, "Neplatná IP adresa."); }

        QueryResult Failed(QueryFailure failure, string message) => new(DateTimeOffset.UtcNow, null, null, failure, message);
    }

    private static async Task<byte[]> ReceiveMessageAsync(UdpClient udp, CancellationToken ct)
    {
        byte[][]? fragments = null;
        uint? messageId = null;
        int received = 0;
        for (int packets = 0; packets < 128; packets++)
        {
            var packet = (await udp.ReceiveAsync(ct).ConfigureAwait(false)).Buffer;
            if (packet.Length < 5) throw new InvalidDataException("Příliš krátký UDP paket.");
            var header = BinaryPrimitives.ReadInt32LittleEndian(packet);
            if (header == -1 && fragments is null) return packet;
            if (header != -2 || packet.Length < 12) throw new InvalidDataException("Neplatná split hlavička.");
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4));
            if ((id & 0x80000000) != 0)
                throw new UnsupportedPacketException("Komprimovaná BZip2 split odpověď není podporována; query je N/A.");
            int total = packet[8], index = packet[9];
            if (total is < 1 or > 64 || index >= total || packet.Length == 12)
                throw new InvalidDataException("Neplatné pořadí nebo počet fragmentů.");
            messageId ??= id;
            fragments ??= new byte[total][];
            if (id != messageId || total != fragments.Length) throw new InvalidDataException("Smíchané split odpovědi.");
            var payload = packet.AsSpan(12).ToArray();
            if (fragments[index] is { } existing)
            {
                if (!existing.AsSpan().SequenceEqual(payload)) throw new InvalidDataException("Rozdílné duplicitní fragmenty.");
                continue;
            }
            fragments[index] = payload;
            received++;
            if (received != total) continue;
            if (fragments.Sum(f => f.Length) > 65536) throw new InvalidDataException("A2S zpráva je příliš velká.");
            var assembled = fragments.SelectMany(f => f).ToArray();
            // Some Source servers include a second single-packet header in the assembled payload.
            if (assembled.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(assembled) == -1) return assembled;
            return [255, 255, 255, 255, .. assembled];
        }
        throw new InvalidDataException("Překročen limit UDP fragmentů.");
    }

    public static ServerInfo ParseInfo(ReadOnlySpan<byte> packet)
    {
        var reader = new PacketReader(packet);
        if (reader.UInt32() != uint.MaxValue || reader.Byte() != 0x49)
            throw new InvalidDataException("Odpověď není Source A2S_INFO (0x49).");
        reader.Byte(); // protocol
        var name = reader.CString();
        var map = reader.CString();
        reader.CString(); // folder
        var game = reader.CString();
        var appId = reader.UInt16();
        var players = reader.Byte();
        var max = reader.Byte();
        reader.Byte(); // bots
        reader.Byte(); // server type
        reader.Byte(); // environment
        var visibility = reader.Byte();
        var vac = reader.Byte();
        if (visibility > 1 || vac > 1 || players > max)
            throw new InvalidDataException("Neplatná základní pole A2S_INFO.");
        if (appId == 2400) reader.Skip(3); // The Ship extension
        var version = reader.CString();
        string keywords = "";
        ulong? gameId = null;
        if (reader.Remaining > 0)
        {
            var edf = reader.Byte();
            if ((edf & 0x80) != 0) reader.Skip(2);
            if ((edf & 0x10) != 0) reader.Skip(8);
            if ((edf & 0x40) != 0) { reader.Skip(2); reader.CString(); }
            if ((edf & 0x20) != 0) keywords = reader.CString();
            if ((edf & 0x01) != 0) gameId = reader.UInt64();
        }
        // Modern DayZ servers may put a placeholder in the legacy 16-bit AppID.
        var extra = appId == 24492 || gameId == 221100 ? DayZKeywords.Parse(keywords) : default;
        return new(name, map, game, players, max, version) { GameClock = extra.Clock, Queue = extra.Queue };
    }

    private sealed class UnsupportedPacketException(string message) : Exception(message);

    private ref struct PacketReader(ReadOnlySpan<byte> bytes)
    {
        private ReadOnlySpan<byte> rest = bytes;
        public int Remaining => rest.Length;
        public void Skip(int count)
        {
            if (rest.Length < count) throw new InvalidDataException("Zkrácená A2S odpověď.");
            rest = rest[count..];
        }
        public byte Byte() { var value = rest.Length > 0 ? rest[0] : throw new InvalidDataException("Zkrácená A2S odpověď."); Skip(1); return value; }
        public ushort UInt16() { if (rest.Length < 2) throw new InvalidDataException("Zkrácené číslo."); var value = BinaryPrimitives.ReadUInt16LittleEndian(rest); Skip(2); return value; }
        public uint UInt32() { if (rest.Length < 4) throw new InvalidDataException("Zkrácená hlavička."); var value = BinaryPrimitives.ReadUInt32LittleEndian(rest); Skip(4); return value; }
        public ulong UInt64() { if (rest.Length < 8) throw new InvalidDataException("Zkrácený GameID."); var value = BinaryPrimitives.ReadUInt64LittleEndian(rest); Skip(8); return value; }
        public string CString()
        {
            int end = rest.IndexOf((byte)0);
            if (end < 0 || end > 8192) throw new InvalidDataException("Neukončený nebo příliš dlouhý řetězec.");
            var value = Encoding.UTF8.GetString(rest[..end]);
            Skip(end + 1);
            return value;
        }
    }
}
