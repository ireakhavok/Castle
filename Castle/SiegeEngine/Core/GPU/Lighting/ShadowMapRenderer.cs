// Folder: SiegeEngine/Core/GPU/Lighting
// File: ShadowMapRenderer.cs
using SiegeEngine.Core.AssetParsing.Model;
using SiegeEngine.Core.Definitions;
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
    /// the primary point light, 2D map for the primary spot light. Games only
    /// place lights and pick a quality preset.
    ///
    /// Cascades are nested WORLD-SPACE orthographic boxes around a texel-snapped
    /// focus (the camera position). They do not fit the camera frustum, so yaw,
    /// pitch, and zoom cannot flip a world point in or out of shadow.
    /// </summary>
    public unsafe class ShadowMapRenderer : IDisposable
    {
        private const int GL_FRAMEBUFFER_BINDING = 0x8CA6;
        private const int GL_VIEWPORT = 0x0BA2;
        private const int GL_NONE = 0;
        private const int GL_FRONT = 0x0404;

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

            if (sunMaps)
            {
                int cascadeCount = CascadeCount(frame.ShadowQuality);
                EnsureAtlas(atlasSize);
                frame.CascadeCount = cascadeCount;
                frame.ShadowAtlas = _atlasDepth;

                float far = MathF.Max(frame.ShadowDistance, 2048f);
                ComputeCascades(frame, cameraPos, far, cascadeCount, atlasSize, casters);

                // Models render two-sided. Culling front faces emptied the
                // atlas (winding is not reliable) and sun shadows vanished.
                _rc.Disable(_e.CullFace);

                int tile = atlasSize / 2;
                _rc.BindFramebuffer(_e.Framebuffer, _atlasFbo);
                _rc.DrawBuffer(GL_NONE);
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

            // Point / spot cubemaps see every side; two-sided depth.
            _rc.Disable(_e.CullFace);

            if (frame.SpotCount > 0 && frame.Spots[0].CastShadows && frame.Spots[0].Technique == ShadowTechnique.ShadowMap)
            {
                int size = Math.Max(atlasSize / 2, 512);
                EnsureSpot(size);
                frame.SpotShadowMap = _spotDepth;
                frame.SpotVP = BuildSpotVP(frame.Spots[0]);
                _rc.BindFramebuffer(_e.Framebuffer, _spotFbo);
                _rc.DrawBuffer(GL_NONE);
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
                // Light visualization meshes and editor gizmos must not write
                // into the point cubemap or the sun atlas.
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
            // 90-degree GL clip-space perspective so each cubemap face is the
            // light's actual view of the subject, not a DX-style frustum.
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
                _rc.DrawBuffer(GL_NONE);
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
        /// World-covering orthos from the last commit where sun shadows
        /// actually drew (baebf3ea). Cascade 0 is a tight tile around the
        /// camera; later cascades cover the caster centroid so a wrong
        /// camera extract still hits the models.
        /// </summary>
        private void ComputeCascades(LightingFrame frame, Vector3 cameraPos, float far, int cascadeCount, int atlasSize, IReadOnlyList<ShadowCaster> casters)
        {
            Vector3 sceneMin = cameraPos;
            Vector3 sceneMax = cameraPos;
            bool haveCaster = false;
            if (casters != null)
            {
                for (int c = 0; c < casters.Count; c++)
                {
                    Vector3 world = casters[c].ModelMatrix.Translation;
                    if (!haveCaster)
                    {
                        sceneMin = world;
                        sceneMax = world;
                        haveCaster = true;
                    }
                    else
                    {
                        sceneMin = Vector3.Min(sceneMin, world);
                        sceneMax = Vector3.Max(sceneMax, world);
                    }
                }
            }
            Vector3 groundCam = new Vector3(cameraPos.X, cameraPos.Y, 0f);
            Vector3 sceneCenter = haveCaster
                ? new Vector3((sceneMin.X + sceneMax.X) * 0.5f, (sceneMin.Y + sceneMax.Y) * 0.5f, 0f)
                : groundCam;
            float sceneRadius = haveCaster ? Vector3.Distance(sceneMin, sceneMax) * 0.5f + 80f : 256f;
            sceneRadius = MathF.Max(sceneRadius, 256f);
            float worldRadius = Math.Clamp(MathF.Max(far, MathF.Max(sceneRadius * 2f, 2048f)), 1024f, 4096f);

            float[] radii = new float[LightingFrame.MaxCascades];
            if (frame.ShadowQuality == ShadowQuality.Ultra)
            {
                radii[0] = 48f;
                radii[1] = 160f;
                radii[2] = 512f;
                radii[3] = worldRadius;
            }
            else if (frame.ShadowQuality == ShadowQuality.High)
            {
                radii[0] = 64f;
                radii[1] = 192f;
                radii[2] = 640f;
                radii[3] = worldRadius;
            }
            else if (frame.ShadowQuality == ShadowQuality.Low)
            {
                radii[0] = 160f;
                radii[1] = worldRadius;
                radii[2] = worldRadius;
                radii[3] = worldRadius;
            }
            else
            {
                radii[0] = 80f;
                radii[1] = 240f;
                radii[2] = 720f;
                radii[3] = worldRadius;
            }
            frame.CascadeSplits = new Vector4(radii[0], radii[1], radii[2], radii[3]);

            Vector3 lightDir = frame.Sun.Direction.LengthSquared() > 1e-8f
                ? Vector3.Normalize(frame.Sun.Direction)
                : LightingFrame.DefaultSunDirection;
            Vector3 lightUp = MathF.Abs(Vector3.Dot(lightDir, Vector3.UnitZ)) > 0.95f ? Vector3.UnitX : Vector3.UnitZ;

            float casterPad = MathF.Max(sceneRadius + 80f, 120f);
            int tile = Math.Max(atlasSize / 2, 1);
            float zBack = worldRadius + casterPad + 80f;
            float zFwd = worldRadius + casterPad + 80f;

            for (int i = 0; i < cascadeCount; i++)
            {
                float radius = radii[i];
                float texel = MathF.Max((radius * 2f) / tile, 0.05f);
                Vector3 focus = i == 0 && cascadeCount > 1
                    ? SnapToTexel(groundCam, texel)
                    : SnapToTexel(sceneCenter, texel);

                Vector3 eye = focus - lightDir * zBack;
                Matrix4x4 lightView = Matrix4x4.CreateLookAt(eye, focus, lightUp);

                Vector3 lsFocus = Vector3.Transform(focus, lightView);
                lsFocus.X = MathF.Round(lsFocus.X / texel) * texel;
                lsFocus.Y = MathF.Round(lsFocus.Y / texel) * texel;
                Vector3 snappedWorld = TransformByInverse(lightView, new Vector3(lsFocus.X, lsFocus.Y, lsFocus.Z));
                eye = snappedWorld - lightDir * zBack;
                lightView = Matrix4x4.CreateLookAt(eye, snappedWorld, lightUp);

                Matrix4x4 lightProj = CreateGLOrtho(-radius, radius, -radius, radius, 1f, zBack + zFwd);
                frame.CascadeVP[i] = lightView * lightProj;
            }
            for (int i = cascadeCount; i < LightingFrame.MaxCascades; i++)
                frame.CascadeVP[i] = Matrix4x4.Identity;
        }

        private static Vector3 SnapToTexel(Vector3 p, float texel)
        {
            if (texel < 1e-4f) return p;
            return new Vector3(
                MathF.Round(p.X / texel) * texel,
                MathF.Round(p.Y / texel) * texel,
                MathF.Round(p.Z / texel) * texel);
        }

        private static Vector3 TransformByInverse(Matrix4x4 m, Vector3 p)
        {
            if (!Matrix4x4.Invert(m, out Matrix4x4 inv))
                return p;
            return Vector3.Transform(p, inv);
        }

        /// <summary>
        /// OpenGL clip-space ortho (Z in [-1, 1]) expressed as a System.Numerics
        /// row-vector matrix. Matches the engine's SetMatrix4 upload convention
        /// so shader `proj * 0.5 + 0.5` covers the full depth buffer.
        /// </summary>
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
            // baebf3ea: one tight tile + one scene tile. Extra cascades were
            // empty-rejected by the sampler and produced no sun shadows.
            return quality == ShadowQuality.Low ? 1 : 2;
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
            _rc.DrawBuffer(GL_NONE);
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
            _rc.DrawBuffer(GL_NONE);
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
