using Bsp.Common.Geometry;
using Bsp.Common.Tree;
using Bsp.Server.Abstractions;

namespace Bsp.Server.Commands;

public class MeshSubdivideToBrushes : ICommandHandler
{
    private class Processor : ICommandProcessor
    {
        private readonly MeshContext _meshContext;

        public Processor(MeshContext meshContext)
        {
            _meshContext = meshContext;
        }

        public async ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress)
        {
            var mesh = Mesh.FromContext(_meshContext);
            mesh.Content = 1;
            var treeGraph = mesh.ToBsp(ContentOperations.Space3DVisBsp, AreaBuilders.BasicAreaBuilder, AreaBuilders.BasicAreaBuilder);
            var numSubs = 0;
            for (int i = 0; i < treeGraph.Leafs.Count; ++i)
            {
                if (treeGraph.Leafs[i].flags.Flags != mesh.Content) continue;
                numSubs++;
            }
            var meshes = new List<MeshContext>(numSubs);
            for (int i = 0; i < treeGraph.Leafs.Count; ++i)
            {
                if (treeGraph.Leafs[i].flags.Flags != mesh.Content) continue;
                var m = treeGraph.ToMesh(i);
                meshes.Add(m.ToContext());
                // await progress.OnProgress(meshes.Count, numSubs);
            }
            CommandController.Success(stream);
            stream.Write(numSubs);
            foreach (var m in meshes)
            {
                await stream.WriteMesh(m);
            }
            return null;
        }
    }
    public async ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default)
        => new Processor(await stream.TryReadMesh(cancellationToken) ?? throw new CommandHandlerException("Failed to read mesh"));

}