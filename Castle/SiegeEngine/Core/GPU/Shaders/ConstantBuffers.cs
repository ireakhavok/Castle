// Folder: SiegeEngine/Core/GPU/Shaders
// File: ConstantBuffers.cs
using System.Numerics;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class ConstantSlot
    {
        public const int Frame = 0;
        public const int Object = 1;
        public const int Skin = 2;
        public const int Material = 3;
        public const int Light = 4;
        public const int Shadow = 5;
        public const int Ui = 6;
        public const int Post = 7;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FrameCB
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 ViewPos;
        public float Time;
        public int HasTexture;
        public float PadFrame0;
        public float PadFrame1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectCB
    {
        public Matrix4x4 Model;
        public Matrix4x4 NormalMatrix;
        public int HasBones;
        public int ReceiveShadows;
        public int Pad0;
        public int Pad1;
        public float PointSize;
        public float VerticalOffset;
        public float Pad3;
        public float Pad4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SkinCB
    {
        public fixed float BoneTransforms[2048];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MaterialCB
    {
        public int HasOpacity;
        public int OpacitySlots;
        public int DebugTextureOnly;
        public int DebugMaterialIndex;
        public int HasWorldAligned;
        public int MappingMode0;
        public int MappingMode1;
        public int MappingMode2;
        public int MappingMode3;
        public int Pad0;
        public int Pad1;
        public int Pad2;
        public Vector4 Tiling0;
        public Vector4 Tiling1;
        public Vector4 Tiling2;
        public Vector4 Tiling3;
        public Vector4 Offset0;
        public Vector4 Offset1;
        public Vector4 Offset2;
        public Vector4 Offset3;
        public Vector4 Rotation;
        public Vector4 BlendSharpness;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LightCB
    {
        public Vector4 LightDir;
        public Vector4 LightColor;
        public Vector4 AmbientColor;
        public Vector4 ViewPos;
        public float LightIntensity;
        public float AmbientStrength;
        public float SpecularStrength;
        public float Shininess;
        public int PointCount;
        public int SpotCount;
        public int FogMode;
        public int Pad0;
        public Vector4 PointPos0;
        public Vector4 PointPos1;
        public Vector4 PointPos2;
        public Vector4 PointPos3;
        public Vector4 PointColor0;
        public Vector4 PointColor1;
        public Vector4 PointColor2;
        public Vector4 PointColor3;
        public Vector4 PointIntensityRange0;
        public Vector4 PointIntensityRange1;
        public Vector4 PointIntensityRange2;
        public Vector4 PointIntensityRange3;
        public Vector4 SpotPos0;
        public Vector4 SpotPos1;
        public Vector4 SpotDir0;
        public Vector4 SpotDir1;
        public Vector4 SpotColor0;
        public Vector4 SpotColor1;
        public Vector4 SpotIntensityRange0;
        public Vector4 SpotIntensityRange1;
        public Vector4 SpotCone0;
        public Vector4 SpotCone1;
        public Vector4 FogColor;
        public float FogDensity;
        public float FogStart;
        public float FogHeight;
        public float FogHeightFalloff;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShadowCB
    {
        public Matrix4x4 CascadeVP0;
        public Matrix4x4 CascadeVP1;
        public Matrix4x4 CascadeVP2;
        public Matrix4x4 CascadeVP3;
        public Vector4 CascadeSplits;
        public Vector4 CascadeZRange;
        public int ShadowsEnabled;
        public int ReceiveShadows;
        public int CascadeCount;
        public int ShadowSmooth;
        public float ShadowBias;
        public float ShadowAtlasSize;
        public float ShadowStrength;
        public int PointShadowsEnabled;
        public float PointShadowFar;
        public float PointShadowStrength;
        public float Pad0;
        public float Pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UiCB
    {
        public Matrix4x4 Transform;
        public Vector4 Color;
        public Vector4 Color1;
        public Vector4 Color2;
        public Vector4 BorderRadius;
        public Vector4 RectSize;
        public Vector4 BorderColor;
        public float UseTexture;
        public float UseGradient;
        public float UseRounded;
        public float GradientFlip;
        public int GradientAxis;
        public float BorderWidth;
        public float Outline;
        public float Pad0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PostCB
    {
        public Matrix4x4 PrevView;
        public Matrix4x4 PrevProjection;
        public Matrix4x4 InvView;
        public Matrix4x4 InvProjection;
        public Vector4 InvResolution;
        public float Threshold;
        public float Knee;
        public float Exposure;
        public float BloomIntensity;
        public float Contrast;
        public float Saturation;
        public float Temperature;
        public float TargetLuma;
        public float Adapt;
        public float AdaptedLuma;
        public int HasHistory;
        public int HasBloom;
        public int HasPrev;
        public int AutoExposure;
        public int Tonemap;
        public int Steps;
        public float Intensity;
        public int HasDepth;
        public float Unlit;
        public float PolyFactor;
        public float PolyUnits;
        public float LinearDepth;
        public float FarPlane;
        public Vector4 LightPos;
    }
}

