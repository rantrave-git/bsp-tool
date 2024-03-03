// using System.Buffers;
// using System.Runtime.InteropServices;
// using System.Text;
// using Bsp.Common.Geometry;

// namespace Bsp.Server;

// public static class StreamExtensions
// {
//     static readonly int MaxBufferSize = 4096;
//     public static async ValueTask<bool> TryRead(this Stream stream, Memory<byte> dst, CancellationToken cancellationToken = default)
//     {
//         int pos = 0;
//         while (pos < dst.Length)
//         {

//             var len = await stream.ReadAsync(dst[pos..], cancellationToken);
//             if (len == 0) { return false; }
//             pos += len;
//         }
//         return true;
//     }
//     public static async ValueTask<T?> TryRead<T>(this Stream stream, CancellationToken cancellationToken = default) where T : struct
//     {
//         byte[] bytes = ArrayPool<byte>.Shared.Rent(Marshal.SizeOf<T>());
//         try
//         {
//             if (!await stream.TryRead(bytes.AsMemory(0, Marshal.SizeOf<T>()), cancellationToken)) return null;
//             return MemoryMarshal.Cast<byte, T>(bytes)[0];
//         }
//         finally
//         {
//             ArrayPool<byte>.Shared.Return(bytes);
//         }
//     }
//     public static async ValueTask<T[]?> TryReadArray<T>(this Stream stream, CancellationToken cancellationToken = default) where T : struct
//     {
//         int? len = await stream.TryRead<int>(cancellationToken);
//         if (len == null) return null;
//         var byteLen = Marshal.SizeOf<T>() * len.Value;
//         byte[] bytes = ArrayPool<byte>.Shared.Rent(byteLen);
//         try
//         {
//             if (!await stream.TryRead(bytes.AsMemory(0, byteLen), cancellationToken)) return null;
//             var dst = new T[len.Value];
//             bytes.AsSpan(0, byteLen).CopyTo(MemoryMarshal.Cast<T, byte>(dst));
//             return dst;
//         }
//         finally
//         {
//             ArrayPool<byte>.Shared.Return(bytes);
//         }
//     }
//     public static async ValueTask<MeshContext?> TryReadMeshAsync(this Stream stream, CancellationToken cancellationToken = default)
//     {
//         // Console.WriteLine($"{Marshal.SizeOf<VertexData>()} {Marshal.SizeOf<CornerData>()} {Marshal.SizeOf<FaceData>()}");
//         // read vertices
//         var vertices = await stream.TryReadArray<VertexData>(cancellationToken);
//         if (vertices == null) return null;
//         // read corners
//         var corners = await stream.TryReadArray<CornerData>(cancellationToken);
//         if (corners == null) return null;
//         // read faces
//         var faces = await stream.TryReadArray<FaceData>(cancellationToken);
//         if (faces == null) return null;
//         return new MeshContext(vertices, corners, faces);
//     }
//     private static void DoWrite(Stream stream, string message)
//     {
//         var len = Encoding.UTF8.GetByteCount(message);
//         Span<byte> buffer = stackalloc byte[len];
//         Encoding.UTF8.GetBytes(message, buffer);
//         stream.Write(buffer);
//     }
//     private static async ValueTask WriteString(this Stream stream, string message)
//     {
//         var len = Encoding.UTF8.GetByteCount(message);
//         if (len > MaxBufferSize)
//         {
//             await stream.WriteString(message[..(message.Length / 2)]);
//             await Task.Yield();
//             await stream.WriteString(message[(message.Length / 2)..]);
//         }
//         DoWrite(stream, message);
//     }
//     public static async ValueTask WriteMessage(this Stream stream, string message)
//     {
//         var len = Encoding.UTF8.GetByteCount(message);
//         stream.Write(len);
//         await stream.WriteString(message);
//     }
//     public static void Write(this Stream stream, int value)
//     {
//         Span<byte> buffer = stackalloc byte[sizeof(int)];
//         BitConverter.TryWriteBytes(buffer, value);
//         stream.Write(buffer);
//     }
//     public static void Write(this Stream stream, long value)
//     {
//         Span<byte> buffer = stackalloc byte[sizeof(long)];
//         BitConverter.TryWriteBytes(buffer, value);
//         stream.Write(buffer);
//     }
//     public static async ValueTask WriteArray<T>(this Stream stream, Memory<T> array) where T : struct
//     {
//         stream.Write(array.Length);
//         int step = MaxBufferSize / Marshal.SizeOf<T>();
//         int i = 0;
//         while (true)
//         {
//             var end = i + step;
//             if (end >= array.Length)
//             {
//                 Console.WriteLine($"Length {MemoryMarshal.Cast<T, byte>(array.Span[i..]).Length}");
//                 stream.Write(MemoryMarshal.Cast<T, byte>(array.Span[i..]));
//                 return;
//             }
//             Console.WriteLine($"Length {MemoryMarshal.Cast<T, byte>(array.Span[i..end]).Length}");
//             stream.Write(MemoryMarshal.Cast<T, byte>(array.Span[i..end]));
//             i = end;
//             await Task.Yield();
//         }
//     }
//     public static async ValueTask WriteMesh(this Stream stream, MeshContext mesh)
//     {
//         await stream.WriteArray(mesh.Vertices.AsMemory());
//         await stream.WriteArray(mesh.Corners.AsMemory());
//         await stream.WriteArray(mesh.Faces.AsMemory());
//     }
// }
