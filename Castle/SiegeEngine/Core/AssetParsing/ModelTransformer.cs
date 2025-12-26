// Engine.Core.AssetParsing/ModelTransformer.cs
using SiegeEngine.Core.AssetParsing.Model;
using System.Numerics;

namespace SiegeEngine.Core.AssetParsing
{
    public static class ModelTransformer
    {
        public static void ApplyTransformation(FBXModel model, Matrix4x4 transformation)
        {
            foreach (var mesh in model.Meshes)
            {
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var vertex = mesh.Vertices[i];
                    Vector3 position = new Vector3(vertex.X, vertex.Y, vertex.Z);
                    Vector3 normal = new Vector3(vertex.Nx, vertex.Ny, vertex.Nz);

                    position = Vector3.Transform(position, transformation);
                    normal = Vector3.TransformNormal(normal, transformation);

                    mesh.Vertices[i] = new FBXVertex(
                        position.X, position.Y, position.Z,
                        normal.X, normal.Y, normal.Z,
                        vertex.U, vertex.V,
                        vertex.MatIdx
                    );
                }
            }
        }
    }
}