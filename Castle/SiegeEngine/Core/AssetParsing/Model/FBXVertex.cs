namespace SiegeEngine.Core.AssetParsing.Model
{
    public class FBXVertex
    {
        public float X, Y, Z;
        public float Nx, Ny, Nz;
        public float U, V;
        public float MatIdx;
        public float Tx, Ty, Tz;
        public int BoneID0, BoneID1, BoneID2, BoneID3;
        public float Weight0, Weight1, Weight2, Weight3;
        public FBXVertex(float x, float y, float z, float nx, float ny, float nz, float u, float v, float matIdx, float tx = 0, float ty = 0, float tz = 0,
        int boneID0 = -1, int boneID1 = -1, int boneID2 = -1, int boneID3 = -1,
        float weight0 = 0, float weight1 = 0, float weight2 = 0, float weight3 = 0)
        {
            X = x; Y = y; Z = z;
            Nx = nx; Ny = ny; Nz = nz;
            U = u; V = v;
            MatIdx = matIdx;
            Tx = tx; Ty = ty; Tz = tz;
            BoneID0 = boneID0; BoneID1 = boneID1; BoneID2 = boneID2; BoneID3 = boneID3;
            Weight0 = weight0; Weight1 = weight1; Weight2 = weight2; Weight3 = weight3;
        }
    }
}