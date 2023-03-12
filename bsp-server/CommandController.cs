using System.Buffers;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Bsp.Geometry;

namespace Bsp;

class CommandController
{
    enum Command : Int32
    {
        TestEcho = 0x0,
        EchoMesh = 0x1
    }
    private TcpListener? _listener;
    public async Task Start(int port, CancellationToken cancellationToken = default)
    {
        IPAddress localhost = IPAddress.Loopback;
        _listener = new TcpListener(localhost, port);
        _listener.Start();
        Console.WriteLine($"Listening on {port}...");
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine($"Listening for client...");
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);

                _ = Stream(client, cancellationToken);
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine($"[Socket]: {e}");
        }
        finally
        {
            _listener.Stop();
        }
    }
    private async Task<bool> TryRead(Stream stream, byte[] dst, int offset, int length)
    {
        int pos = 0;
        while (pos < length)
        {

            var len = await stream.ReadAsync(dst, offset + pos, length - pos);
            if (len == 0) { return false; }
            pos += len;
        }
        return true;
    }
    private async Task<T[]?> TryReadArray<T>(Stream stream) where T : struct
    {
        byte[] lenBytes = ArrayPool<byte>.Shared.Rent(sizeof(Int32));
        int len = 0;
        try
        {
            if (!await TryRead(stream, lenBytes, 0, sizeof(Int32))) return null;
            len = BitConverter.ToInt32(lenBytes.AsSpan(0, sizeof(Int32)));
            if (len == 0) return Array.Empty<T>();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(lenBytes);
        }
        var byteLen = Marshal.SizeOf<T>() * len;
        byte[] bytes = ArrayPool<byte>.Shared.Rent(byteLen);
        try
        {
            if (!await TryRead(stream, bytes, 0, byteLen)) return null;
            var dst = new T[len];
            bytes.AsSpan(0, byteLen).CopyTo(MemoryMarshal.Cast<T, byte>(dst));
            return dst;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }
    private async Task<MeshContext?> TryReadMesh(Stream stream)
    {
        // read vertices
        var vertices = await TryReadArray<VertexData>(stream);
        if (vertices == null) return null;
        // read corners
        var corners = await TryReadArray<CornerData>(stream);
        if (corners == null) return null;
        // read faces
        var faces = await TryReadArray<FaceData>(stream);
        if (faces == null) return null;
        return new MeshContext(vertices, corners, faces);
    }
    private async Task Stream(TcpClient client, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Client connected {client.Client.RemoteEndPoint}");
        byte[] recvBuffer = ArrayPool<byte>.Shared.Rent(256);
        var headerLength = sizeof(Int32);
        try
        {
            var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested)
            {
                // read header
                if (!await TryRead(stream, recvBuffer, 0, headerLength)) break;
                var command = (Command)BitConverter.ToInt32(recvBuffer.AsSpan(0, headerLength));
                Console.WriteLine($"Received command {command}");
                switch (command)
                {
                    default:
                        break;
                    case Command.EchoMesh:
                        // read mesh
                        var m = await TryReadMesh(stream);
                        if (m == null) throw new IndexOutOfRangeException("Invalid format");
                        using (var ms = new MemoryStream())
                        using (var bw = new BinaryWriter(ms))
                        {
                            bw.Write(m.Vertices.Length);
                            ms.Write(MemoryMarshal.Cast<VertexData, byte>(m.Vertices));
                            bw.Write(m.Corners.Length);
                            ms.Write(MemoryMarshal.Cast<CornerData, byte>(m.Corners));
                            bw.Write(m.Faces.Length);
                            ms.Write(MemoryMarshal.Cast<FaceData, byte>(m.Faces));

                            ms.Flush();
                            ms.Position = 0;
                            var len = 0;
                            while ((len = ms.Read(recvBuffer, 0, recvBuffer.Length)) != 0)
                            {
                                await stream.WriteAsync(recvBuffer, 0, len);
                            }
                        }
                        break;
                    case Command.TestEcho:
                        var s = await TryReadArray<byte>(stream);
                        if (s != null)
                        {
                            Console.WriteLine($"Received {Encoding.UTF8.GetString(s)}");
                            await stream.WriteAsync(s, 0, s.Length);
                        }
                        else
                        {
                            Console.WriteLine("$Invalid data received");
                        }
                        break;
                }
            }
        }
        catch (IndexOutOfRangeException) { }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            Console.WriteLine($"Client disconnected {client.Client.RemoteEndPoint}");
            client.Dispose();
            ArrayPool<byte>.Shared.Return(recvBuffer);
        }
    }
}