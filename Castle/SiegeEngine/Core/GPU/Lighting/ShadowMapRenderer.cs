// Folder: SiegeEngine/Core/GPU/Lighting
// File: ShadowMapRenderer.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Lighting
{
    public struct ShadowCaster
    {
        public Matrix4x4 ModelMatrix;
        public ModelManager.ModelData ModelData;
        public Matrix4x4[] BoneMatrices;
        public bool HasBones;
        public bool CastShadows;
    }

    /// <summary>
    /// Core-owned shadow map generation. CSM atlas for the sun, cubemap for
    /// the primary point light, 2D map for the primary spot light.
    ///
    /// Sun cascades are nested WORLD-SPACE orthographic boxes on the XY ground
    /// plane (Z-up). Shaders pick a tile by light-clip containment, not by
    /// camera view-depth. Terrain is a receiver only — it is not a caster —
    /// so the atlas holds model umbras instead of ground self-depth.
    /// </summary>
    public unsafe class ShadowMapRenderer : IDisposable
    {
        private const int GL_FRAMEBUFFER_BINDING = 0x8CA6;
        private const int GL_VIEWPORT = 0x0BA2;
        private const int GL_NONE = 0;
        private const int GL_BACK = 0x0405;

        private readonly IRenderContext _rc;
        private readonly AbstractRenderEnums _e;
        private ShaderProgram _depthShader;
        private bool _disposed;

        private int _atlasSize;
        private uint _atlasFbo;
        private uint _atlasDepth;
        private uint _spotFbo;
        private uint _spotDepth;
        private uint _pointFbo;
        private uint _pointDepth;
        private int _spotSize;
        private int _pointSize;

        private int _savedFbo;
        private int _savedVpX, _savedVpY, _savedVpW, _savedVpH;

        public ShadowMapRenderer(IRenderContext renderContext)
        {
            _rc = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _e = _rc.Enums;
            _depthShader = new ShaderProgram(_rc, ShadowShaders.DepthVertex, ShadowShaders.DepthFragment);
        }

        public void Render(LightingFrame frame, IReadOnlyList<ShadowCaster> casters, Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPos)
        {
            if (_disposed || frame == null)
                return;

            bool sunMaps = frame.Sun.CastShadows && frame.Sun.Technique == ShadowTechnique.ShadowMap && frame.Sun.Intensity > 0.001f;
            bool pointMaps = frame.PointCount > 0 && frame.Points[0].CastShadows && frame.Points[0].Technique == ShadowTechnique.ShadowMap;
            bool spotMaps = frame.SpotCount > 0 && frame.Spots[0].CastShadows && frame.Spots[0].Technique == ShadowTechnique.ShadowMap;
            if (frame.ShadowQuality == ShadowQuality.Off || (!sunMaps && !pointMaps && !spotMaps))
            {
                frame.ShadowsReady = false;
                return;
            }

            Capture();
            int atlasSize = AtlasSize(frame.ShadowQuality);
            _rc.ColorMask(false, false, false, false);
            _rc.Enable(_e.DepthTest);
            _rc.DepthMask(true);
            _rc.DepthFunc(_e.Less);
            _rc.Disable(_e.CullFace);

            if (sunMaps)
            {
                int cascadeCount = CascadeCount(frame.ShadowQuality);
                EnsureAtlas(atlasSize);
                frame.CascadeCount = cascadeCount;
                frame.ShadowAtlas = _atlasDepth;

                float far = frame.ShadowDistance > 1f ? frame.ShadowDistance : 1024f;
                ComputeCascades(frame, view, projection, cameraPos, far, cascadeCount, atlasSize, casters);

                int tile = atlasSize / 2;
                _rc.BindFramebuffer(_e.Framebuffer, _atlasFbo);
                BindDepthOnly();
                _rc.Clear(_e.DepthBufferBit);

                for (int i = 0; i < cascadeCount; i++)
                {
                    int x = (i % 2) * tile;
                    int y = (i / 2) * tile;
                    _rc.Viewport(x, y, (uint)tile, (uint)tile);
                    DrawCasters(frame.CascadeVP[i], casters, linearDepth: false, lightPos: default, farPlane: 1f);
                }
            }
            else
            {
                frame.CascadeCount = 0;
                frame.ShadowAtlas = 0;
            }

            if (frame.SpotCount > 0 && frame.Spots[0].CastShadows && frame.Spots[0].Technique == ShadowTechnique.ShadowMap)
            {
                int size = Math.Max(atlasSize / 2, 512);
                EnsureSpot(size);
                frame.SpotShadowMap = _spotDepth;
                frame.SpotVP = BuildSpotVP(frame.Spots[0]);
                _rc.BindFramebuffer(_e.Framebuffer, _spotFbo);
                BindDepthOnly();
                _rc.Viewport(0, 0, (uint)size, (uint)size);
                _rc.Clear(_e.DepthBufferBit);
                DrawCasters(frame.SpotVP, casters, linearDepth: false, lightPos: default, farPlane: 1f);
            }

            if (frame.PointCount > 0 && frame.Points[0].CastShadows && frame.Points[0].Technique == ShadowTechnique.ShadowMap)
            {
                int size = frame.ShadowQuality switch
                {
                    ShadowQuality.Low => 512,
                    ShadowQuality.High => 2048,
                    ShadowQuality.Ultra => 2048,
                    _ => 1024
                };
                EnsurePoint(size);
                frame.PointShadowCube = _pointDepth;
                RenderPointFaces(frame.Points[0], casters, size);
            }

            _rc.CullFace(_e.Back);
            _rc.ColorMask(true, true, true, true);
            Restore();
            frame.ShadowsReady = true;
        }

        public static List<ShadowCaster> CollectCasters(IReadOnlyList<Entity> entities)
        {
            var list = new List<ShadowCaster>();
            if (entities == null) return list;
            var modelManager = ModelManager.Instance;

            foreach (var entity in entities)
            {
                var modelComp = entity.GetComponent<ModelComponent>();
                var physics = entity.GetComponent<PhysicsComponent>();
                if (modelComp == null || physics == null)
                    continue;
                if (!modelComp.CastShadows)
                    continue;
                if (modelComp.Material != null && !modelComp.Material.CastShadows)
                    continue;
                if (entity.GetComponent<LightComponent>() != null)
                    continue;
                if (entity.GetComponent<PreviewComponent>() != null)
                    continue;
                if (IsEditorHelperKey(modelComp.Key))
                    continue;

                string modelKey = modelComp.Key?.ToLowerInvariant();
                FBXModel fbxModel = modelComp.Model;
                ModelManager.ModelData modelData = null;
                void TryKey(ModelManager mgr, string key)
                {
                    if (mgr == null || string.IsNullOrEmpty(key) || modelData != null) return;
                    if (fbxModel == null)
                        mgr.TryGetModel(key, out fbxModel);
                    mgr.TryGetModelData(key, out modelData);
                }
                void TryAllKeys(ModelManager mgr)
                {
                    TryKey(mgr, modelKey);
                    TryKey(mgr, modelComp.Key);
                    if (!string.IsNullOrEmpty(modelKey) && !modelKey.EndsWith("_pack"))
                        TryKey(mgr, modelKey + "_pack");
                    if (!string.IsNullOrEmpty(modelKey) && modelKey.EndsWith("_pack"))
                        TryKey(mgr, modelKey.Substring(0, modelKey.Length - 5));
                }
                TryAllKeys(modelManager);
                if (modelData == null)
                    continue;

                float unitScale = fbxModel != null ? fbxModel.UnitToMeters : 0.01f;
                Matrix4x4 modelMatrix =
                    Matrix4x4.CreateScale(unitScale * physics.Scale) *
                    Matrix4x4.CreateTranslation(-physics.LocalCentreOfMass) *
                    Matrix4x4.CreateFromQuaternion(physics.Rotation) *
                    Matrix4x4.CreateTranslation(physics.WorldCentreOfMass);

                list.Add(new ShadowCaster
                {
                    ModelMatrix = modelMatrix,
                    ModelData = modelData,
                    BoneMatrices = modelComp.BoneMatrices,
                    HasBones = modelComp.BoneMatrices != null && modelComp.BoneMatrices.Length > 0 && fbxModel != null && fbxModel.HasSkin,
                    CastShadows = true
                });
            }
            return list;
        }

        private static bool IsEditorHelperKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            string k = key.ToLowerInvariant();
            return k.Contains("gizmo")
                || k.Contains("helper")
                || k.Contains("preview")
                || k.Contains("widget")
                || k.Contains("axis")
                || k.Contains("light_icon")
                || k.Contains("lighticon")
                || k.Contains("editoronly")
                || k.Contains("editor_only")
                || k.StartsWith("gizmo_")
                || k.EndsWith("_gizmo");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _depthShader?.Dispose();
            _depthShader = null;
            DeleteFbo(ref _atlasFbo);
            DeleteTex(ref _atlasDepth);
            DeleteFbo(ref _spotFbo);
            DeleteTex(ref _spotDepth);
            DeleteFbo(ref _pointFbo);
            DeleteTex(ref _pointDepth);
        }

        private void DrawCasters(Matrix4x4 lightVp, IReadOnlyList<ShadowCaster> casters, bool linearDepth, Vector3 lightPos, float farPlane)
        {
            if (casters == null) return;
            _depthShader.Use();
            _depthShader.SetMatrix4("uLightVP", lightVp);
            _depthShader.SetUniform("uLinearDepth", linearDepth ? 1 : 0);
            _depthShader.SetUniform("uLightPos", lightPos.X, lightPos.Y, lightPos.Z);
            _depthShader.SetUniform("uFarPlane", farPlane > 0f ? farPlane : 1f);
            foreach (var caster in casters)
            {
                if (caster.ModelData?.MeshRenders == null) continue;
                _depthShader.SetMatrix4("uModel", caster.ModelMatrix);
                _depthShader.SetUniform("uHasBones", caster.HasBones ? 1 : 0);
                if (caster.HasBones && caster.BoneMatrices != null)
                {
                    _depthShader.SetMatrix4Array("uBoneTransforms", caster.BoneMatrices);
                    _depthShader.SetMatrix4Array("uBoneMatrices", caster.BoneMatrices);
                }
                foreach (var mmr in caster.ModelData.MeshRenders)
                {
                    _rc.BindVertexArray(mmr.Vao);
                    _rc.DrawElements(_e.Triangles, mmr.IndexCount, _e.UnsignedInt, null);
                }
            }
            _rc.BindVertexArray(0);
        }

        private void RenderPointFaces(GpuPointLight light, IReadOnlyList<ShadowCaster> casters, int size)
        {
            float near = 0.05f;
            float far = MathF.Max(light.Range, 1f);
            Matrix4x4 proj = CreateGLPerspective(MathF.PI / 2f, 1f, near, far);
            Vector3[] targets =
            {
                light.Position + Vector3.UnitX,
                light.Position - Vector3.UnitX,
                light.Position + Vector3.UnitY,
                light.Position - Vector3.UnitY,
                light.Position + Vector3.UnitZ,
                light.Position - Vector3.UnitZ
            };
            Vector3[] ups =
            {
                -Vector3.UnitY, -Vector3.UnitY,
                Vector3.UnitZ, -Vector3.UnitZ,
                -Vector3.UnitY, -Vector3.UnitY
            };
            int face0 = _e.TextureCubeMapPositiveX;
            for (int face = 0; face < 6; face++)
            {
                _rc.BindFramebuffer(_e.Framebuffer, _pointFbo);
                _rc.FramebufferTexture2D(_e.Framebuffer, _e.DepthAttachment, face0 + face, _pointDepth, 0);
                BindDepthOnly();
                _rc.Viewport(0, 0, (uint)size, (uint)size);
                _rc.Clear(_e.DepthBufferBit);
                Matrix4x4 view = Matrix4x4.CreateLookAt(light.Position, targets[face], ups[face]);
                DrawCasters(view * proj, casters, linearDepth: true, lightPos: light.Position, farPlane: far);
            }
        }

        private static Matrix4x4 BuildSpotVP(GpuSpotLight spot)
        {
            Vector3 dir = spot.Direction.LengthSquared() > 1e-8f ? Vector3.Normalize(spot.Direction) : Vector3.UnitY;
            Vector3 up = MathF.Abs(Vector3.Dot(dir, Vector3.UnitZ)) > 0.95f ? Vector3.UnitX : Vector3.UnitZ;
            Matrix4x4 view = Matrix4x4.CreateLookAt(spot.Position, spot.Position + dir, up);
            float outer = MathF.Acos(Math.Clamp(spot.OuterConeCos, 0.01f, 0.99f));
            float fov = MathF.Max(outer * 2f, 0.1f);
            Matrix4x4 proj = CreateGLPerspective(fov, 1f, 0.05f, MathF.Max(spot.Range, 1f));
            return view * proj;
        }

        /// <summary>
        /// World-covering orthos. Cascade 0 is a tight high-detail tile around
        /// the camera on the ground plane; later cascades cover the scene.
        /// Shaders pick by whether the world point is inside a tile.
        /// </summary>
        private void ComputeCascades(LightingFrame frame, Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPos, float far, int cascadeCount, int atlasSize, IReadOnlyList<ShadowCaster> casters)
        {
            float camNear = ExtractPerspectiveNear(projection, 0.1f);
            float camFar = far > camNear + 1f ? far : camNear + 1f;

            float[] splits = new float[LightingFrame.MaxCascades];
            ComputePracticalSplits(camNear, camFar, cascadeCount, splits);
            frame.CascadeSplits = new Vector4(splits[0], splits[1], splits[2], splits[3]);

            Vector3 lightDir = frame.Sun.Direction.LengthSquared() > 1e-8f
                ? Vector3.Normalize(frame.Sun.Direction)
                : LightingFrame.DefaultSunDirection;
            Vector3 lightUp = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitZ)) > 0.95f ? Vector3.UnitX : Vector3.UnitZ;

            float casterPull = 0f;
            if (casters != null)
            {
                for (int c = 0; c < casters.Count; c++)
                {
                    Vector3 world = casters[c].ModelMatrix.Translation;
                    float along = Vector3.Dot(cameraPos - world, lightDir);
                    if (along > casterPull)
                        casterPull = along;
                }
            }

            int tile = Math.Max(atlasSize / 2, 1);
            Vector3[] corners = new Vector3[8];
            float sliceNear = camNear;

            for (int i = 0; i < cascadeCount; i++)
            {
                float sliceFar = splits[i];
                ExtractSliceCorners(view, projection, cameraPos, sliceNear, sliceFar, corners);
                sliceNear = sliceFar;

                Vector3 center = Vector3.Zero;
                for (int c = 0; c < 8; c++)
                    center += corners[c];
                center *= 0.125f;

                float radius = 0f;
                for (int c = 0; c < 8; c++)
                    radius = MathF.Max(radius, Vector3.Distance(corners[c], center));
                radius = MathF.Max(radius, 1f);
                radius = MathF.Ceiling(radius * 16f) / 16f;

                float texel = MathF.Max((radius * 2f) / tile, 0.001f);
                float zBack = radius + MathF.Max(casterPull, radius);
                float zFwd = radius + 8f;

                Vector3 eye = center - lightDir * zBack;
                Matrix4x4 lightView = Matrix4x4.CreateLookAt(eye, center, lightUp);

                Vector3 lsFocus = Vector3.Transform(center, lightView);
                lsFocus.X = MathF.Round(lsFocus.X / texel) * texel;
                lsFocus.Y = MathF.Round(lsFocus.Y / texel) * texel;
                Vector3 snappedWorld = TransformByInverse(lightView, new Vector3(lsFocus.X, lsFocus.Y, lsFocus.Z));
                eye = snappedWorld - lightDir * zBack;
                lightView = Matrix4x4.CreateLookAt(eye, snappedWorld, lightUp);

                Matrix4x4 lightProj = CreateGLOrtho(-radius, radius, -radius, radius, 0.1f, zBack + zFwd);
                frame.CascadeVP[i] = lightView * lightProj;
            }
            for (int i = cascadeCount; i < LightingFrame.MaxCascades; i++)
                frame.CascadeVP[i] = Matrix4x4.Identity;
        }

        private static void ComputePracticalSplits(float near, float far, int cascadeCount, float[] splits)
        {
            int count = Math.Clamp(cascadeCount, 1, LightingFrame.MaxCascades);
            const float lambda = 0.75f;
            for (int i = 0; i < LightingFrame.MaxCascades; i++)
                splits[i] = far;
            for (int i = 1; i <= count; i++)
            {
                float p = i / (float)count;
                float log = near * MathF.Pow(far / MathF.Max(near, 0.01f), p);
                float uniform = near + (far - near) * p;
                splits[i - 1] = log * lambda + uniform * (1f - lambda);
            }
        }

        private static void ExtractSliceCorners(Matrix4x4 view, Matrix4x4 projection, Vector3 cameraPos, float near, float far, Vector3[] corners)
        {
            float tanHalfV = projection.M22 > 1e-5f ? 1f / projection.M22 : 1f;
            float tanHalfH = projection.M11 > 1e-5f ? 1f / projection.M11 : tanHalfV;
            float nh = tanHalfV * near;
            float nw = tanHalfH * near;
            float fh = tanHalfV * far;
            float fw = tanHalfH * far;

            if (!Matrix4x4.Invert(view, out Matrix4x4 invView))
            {
                Vector3 fallback = cameraPos;
                for (int i = 0; i < 8; i++)
                    corners[i] = fallback;
                return;
            }

            Vector3[] viewCorners =
            {
                new Vector3(-nw,  nh, -near),
                new Vector3( nw,  nh, -near),
                new Vector3( nw, -nh, -near),
                new Vector3(-nw, -nh, -near),
                new Vector3(-fw,  fh, -far),
                new Vector3( fw,  fh, -far),
                new Vector3( fw, -fh, -far),
                new Vector3(-fw, -fh, -far)
            };
            for (int i = 0; i < 8; i++)
                corners[i] = Vector3.Transform(viewCorners[i], invView);
        }

        private static float ExtractPerspectiveNear(Matrix4x4 projection, float fallback)
        {
            float m33 = projection.M33;
            float m43 = projection.M43;
            if (MathF.Abs(m33) < 1e-8f)
                return fallback;
            float near = m43 / m33;
            if (near <= 0.0001f || near > 100f)
                return fallback;
            return near;
        }

        private static Vector3 TransformByInverse(Matrix4x4 m, Vector3 p)
        {
            if (!Matrix4x4.Invert(m, out Matrix4x4 inv))
                return p;
            return Vector3.Transform(p, inv);
        }

        private static Matrix4x4 CreateGLOrtho(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            float rl = right - left;
            float tb = top - bottom;
            float fn = zFar - zNear;
            if (MathF.Abs(rl) < 1e-5f) rl = 1f;
            if (MathF.Abs(tb) < 1e-5f) tb = 1f;
            if (MathF.Abs(fn) < 1e-5f) fn = 1f;

            Matrix4x4 m = Matrix4x4.Identity;
            m.M11 = 2f / rl;
            m.M22 = 2f / tb;
            m.M33 = -2f / fn;
            m.M41 = -(right + left) / rl;
            m.M42 = -(top + bottom) / tb;
            m.M43 = -(zFar + zNear) / fn;
            m.M44 = 1f;
            return m;
        }

        private static Matrix4x4 CreateGLPerspective(float fovY, float aspect, float zNear, float zFar)
        {
            float f = 1f / MathF.Tan(fovY * 0.5f);
            float fn = zFar - zNear;
            if (MathF.Abs(fn) < 1e-5f) fn = 1f;
            Matrix4x4 m = new Matrix4x4();
            m.M11 = f / MathF.Max(aspect, 0.0001f);
            m.M22 = f;
            m.M33 = -(zFar + zNear) / fn;
            m.M34 = -1f;
            m.M43 = -(2f * zFar * zNear) / fn;
            return m;
        }

        private static int CascadeCount(ShadowQuality quality)
        {
            return quality switch
            {
                ShadowQuality.Low => 2,
                ShadowQuality.High => 4,
                ShadowQuality.Ultra => 4,
                _ => 4
            };
        }

        private static int AtlasSize(ShadowQuality quality)
        {
            return quality switch
            {
                ShadowQuality.Low => 1024,
                ShadowQuality.High => 4096,
                ShadowQuality.Ultra => 4096,
                _ => 2048
            };
        }

        private void BindDepthOnly()
        {
            _rc.DrawBuffer(GL_NONE);
            _rc.ReadBuffer(GL_NONE);
        }

        private void EnsureAtlas(int size)
        {
            if (_atlasFbo != 0 && _atlasSize == size)
                return;
            DeleteFbo(ref _atlasFbo);
            DeleteTex(ref _atlasDepth);
            _atlasSize = size;
            _atlasDepth = CreateDepthTex(size, size, _e.Texture2D);
            _rc.GenFramebuffers(1, out _atlasFbo);
            _rc.BindFramebuffer(_e.Framebuffer, _atlasFbo);
            _rc.FramebufferTexture2D(_e.Framebuffer, _e.DepthAttachment, _e.Texture2D, _atlasDepth, 0);
            BindDepthOnly();
        }

        private void EnsureSpot(int size)
        {
            if (_spotFbo != 0 && _spotSize == size)
                return;
            DeleteFbo(ref _spotFbo);
            DeleteTex(ref _spotDepth);
            _spotSize = size;
            _spotDepth = CreateDepthTex(size, size, _e.Texture2D);
            _rc.GenFramebuffers(1, out _spotFbo);
            _rc.BindFramebuffer(_e.Framebuffer, _spotFbo);
            _rc.FramebufferTexture2D(_e.Framebuffer, _e.DepthAttachment, _e.Texture2D, _spotDepth, 0);
            BindDepthOnly();
        }

        private void EnsurePoint(int size)
        {
            if (_pointFbo != 0 && _pointSize == size)
                return;
            DeleteFbo(ref _pointFbo);
            DeleteTex(ref _pointDepth);
            _pointSize = size;
            _rc.GenTextures(1, out _pointDepth);
            _rc.BindTexture(_e.TextureCubeMap, _pointDepth);
            for (int face = 0; face < 6; face++)
            {
                _rc.TexImage2D(_e.TextureCubeMapPositiveX + face, 0, _e.DepthComponent24, (uint)size, (uint)size, 0, _e.DepthComponent, _e.UnsignedInt, null);
            }
            _rc.TexParameter(_e.TextureCubeMap, _e.TextureMinFilter, _e.Nearest);
            _rc.TexParameter(_e.TextureCubeMap, _e.TextureMagFilter, _e.Nearest);
            _rc.TexParameter(_e.TextureCubeMap, _e.TextureWrapS, _e.ClampToEdge);
            _rc.TexParameter(_e.TextureCubeMap, _e.TextureWrapT, _e.ClampToEdge);
            _rc.TexParameter(_e.TextureCubeMap, _e.TextureWrapR, _e.ClampToEdge);
            _rc.GenFramebuffers(1, out _pointFbo);
        }

        private uint CreateDepthTex(int width, int height, int target)
        {
            _rc.GenTextures(1, out uint tex);
            _rc.BindTexture(target, tex);
            _rc.TexImage2D(target, 0, _e.DepthComponent24, (uint)width, (uint)height, 0, _e.DepthComponent, _e.UnsignedInt, null);
            _rc.TexParameter(target, _e.TextureMinFilter, _e.Nearest);
            _rc.TexParameter(target, _e.TextureMagFilter, _e.Nearest);
            _rc.TexParameter(target, _e.TextureWrapS, _e.ClampToEdge);
            _rc.TexParameter(target, _e.TextureWrapT, _e.ClampToEdge);
            return tex;
        }

        private void Capture()
        {
            _rc.GetInteger(GL_FRAMEBUFFER_BINDING, out _savedFbo);
            int* vp = stackalloc int[4];
            _rc.GetInteger(GL_VIEWPORT, vp);
            _savedVpX = vp[0];
            _savedVpY = vp[1];
            _savedVpW = vp[2];
            _savedVpH = vp[3];
        }

        private void Restore()
        {
            _rc.BindFramebuffer(_e.Framebuffer, (uint)Math.Max(_savedFbo, 0));
            if (_savedFbo <= 0)
            {
                _rc.DrawBuffer(GL_BACK);
                _rc.ReadBuffer(GL_BACK);
            }
            _rc.Viewport(_savedVpX, _savedVpY, (uint)Math.Max(_savedVpW, 1), (uint)Math.Max(_savedVpH, 1));
            _rc.ColorMask(true, true, true, true);
            _rc.DepthMask(true);
            _rc.Enable(_e.DepthTest);
            _rc.CullFace(_e.Back);
        }

        private void DeleteFbo(ref uint fbo)
        {
            if (fbo == 0) return;
            uint id = fbo;
            _rc.DeleteFramebuffers(1, &id);
            fbo = 0;
        }

        private void DeleteTex(ref uint tex)
        {
            if (tex == 0) return;
            _rc.DeleteTexture(tex);
            tex = 0;
        }
    }
}
