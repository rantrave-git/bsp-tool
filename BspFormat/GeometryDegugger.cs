using System.Net.Sockets;

namespace Bsp.BspFormat;

public static class GeometryDegugger
{
    public static void SendContext(MeshContext context)
    {
        using var client = new TcpClient("127.0.0.1", 22332);
        using var stream = client.GetStream();
        stream.Write(0);
        stream.WriteMesh(context);
    }

}