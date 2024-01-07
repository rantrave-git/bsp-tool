using Bsp.Common.Geometry;
using Bsp.Common.Tree;
using Bsp.Server.Abstractions;

namespace Bsp.Server.Commands;

public class MeshSubdivideToBrushes : ICommandHandler
{
    private class Processor : ICommandProcessor
    {
        private readonly MeshContext _meshContext;
        private readonly long _content;

        public Processor(MeshContext meshContext, long content)
        {
            _meshContext = meshContext;
            _content = content;
        }

        public async ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress)
        {
            var mesh = Mesh.FromContext(_meshContext);
            var content = 1;
            var treeGraph = mesh.ToBsp(content, ContentOperations.Space3DVisBsp, AreaBuilders.BasicAreaBuilder, AreaBuilders.BasicAreaBuilder);
            var numSubs = 0;
            for (int i = 0; i < treeGraph.Leafs.Count; ++i)
            {
                if (treeGraph.Leafs[i].flags.Flags != content) continue;
                numSubs++;
            }
            var meshes = new List<MeshContext>(numSubs);
            for (int i = 0; i < treeGraph.Leafs.Count; ++i)
            {
                if (treeGraph.Leafs[i].flags.Flags != content) continue;
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
    {

        var context = await stream.TryReadMesh(cancellationToken);
        if (context is null)
        {
            throw new CommandHandlerException("Failed to read mesh");
        }
        // read content
        var content = await stream.TryRead<long>(cancellationToken);
        if (content == null) throw new CommandHandlerException("Failed to read mesh");
        return new Processor(context, content.Value);
    }
}