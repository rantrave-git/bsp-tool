using System.Numerics;
using Bsp.BspFormat;
using Bsp.Server.Abstractions;

namespace Bsp.Server.Commands;

public class MeshContextTest : ICommandHandler
{
    private class Processor : ICommandProcessor
    {
        private readonly MeshContext _meshContext;

        public Processor(int seed)
        {
            var verts = new VertexData[] {
                new () { Pos = new (0.0f, 0.0f, 0.0f), Flags = 0},
                new () { Pos = new (0.0f, 0.0f, 1.0f), Flags = 0},
                new () { Pos = new (0.0f, 1.0f, 1.0f), Flags = 0},
                new () { Pos = new (0.0f, 1.0f, 0.0f), Flags = 0},
            };
            var corners = new CornerData[] {
                new () { Vertex = 0 },
                new () { Vertex = 1 },
                new () { Vertex = 2 },
                new () { Vertex = 3 },
            };
            var faces = new FaceData[] {
                new () { LoopStart = 0, LoopTotal = 4, Flags = 0, Material = 0 }
            };
            _meshContext = new MeshContext(verts, corners, faces);
        }

        public async ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress)
        {
            CommandController.Success(stream);
            await stream.WriteMeshAsync(_meshContext);
            return null;
        }
    }
    public async ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default)
        => new Processor(0);

}

public class MeshContextTestFactory : ICommandHandlerFactory
{
    public ICommandHandler CreateCommandHandler() => new MeshContextTest();
}