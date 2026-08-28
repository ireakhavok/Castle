// Folder: SiegeEngine/Core/Rendering
// File: TerrainRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Lighting;
using SiegeEngine.Core.GPU.Shaders;
using SiegeEngine.Core.Terrain;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe sealed class TerrainRenderer : IDisposable
    {
        // Terrain-only program. SceneShader stays untouched so ModelRenderer
        // does not pick up gl_FragDepth or any bias.
        //
        // Fill is pushed away from the camera by a slope-scaled window-Z
        // offset (same math as glPolygonOffset). Lattice is drawn at true
        // depth with the test on. No constant NDC pull - that is what
        // punched through hills once the camera backed up.
        private const string TerrainVertex = @"#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;
layout(location = 2) in vec2 aUV;
out vec4 vColor;
out vec2 vUV;
out vec3 vWorldPos;
out vec4 vViewPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
void main() {
    vec4 world = uModel * vec4(aPosition, 1.0);
    vWorldPos = world.xyz;
    vViewPos = uView * world;
    gl_Position = uProjection * vViewPos;
    vColor = aColor;
    vUV = aUV;
}";

        private const string TerrainFragment = @"#version 330 core
in vec4 vColor;
in vec2 vUV;
in vec3 vWorldPos;
in vec4 vViewPos;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform int uHasTexture;
uniform float uPolyFactor;
uniform float uPolyUnits;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uAmbientColor;
uniform float uAmbientStrength;
uniform int uShadowsEnabled;
uniform int uReceiveShadows;
uniform int uCascadeCount;
uniform mat4 uCascadeVP[4];
uniform vec4 uCascadeSplits;
uniform sampler2D uShadowAtlas;
uniform float uShadowBias;
uniform float uShadowNormalBias;
uniform float uShadowAtlasSize;
uniform int uFogMode;
uniform vec3 uFogColor;
uniform float uFogDensity;
uniform float uFogHeight;
uniform float uFogHeightFalloff;
float SampleCascadeShadow(vec3 worldPos, vec3 normal) {
    if (uShadowsEnabled == 0 || uReceiveShadows == 0 || uCascadeCount <= 0)
        return 1.0;
    float viewZ = abs(vViewPos.z);
    int cascade = 0;
    if (uCascadeCount > 1 && viewZ > uCascadeSplits.x) cascade = 1;
    if (uCascadeCount > 2 && viewZ > uCascadeSplits.y) cascade = 2;
    if (uCascadeCount > 3 && viewZ > uCascadeSplits.z) cascade = 3;
    vec3 offsetPos = worldPos + normal * uShadowNormalBias;
    vec4 lightClip = uCascadeVP[cascade] * vec4(offsetPos, 1.0);
    vec3 proj = lightClip.xyz / max(lightClip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x <= 0.0 || proj.x >= 1.0 || proj.y <= 0.0 || proj.y >= 1.0 || proj.z > 1.0)
        return 1.0;
    float cell = 0.5;
    vec2 atlasUv = vec2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell + proj.xy * cell;
    float closest = texture(uShadowAtlas, atlasUv).r;
    return (proj.z - uShadowBias > closest) ? 0.35 : 1.0;
}
void main() {
    vec4 albedo = vColor;
    if (uHasTexture == 1) {
        if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
            albedo = texture(uTexture, vUV);
        } else {
            discard;
        }
    }
    vec3 dx = dFdx(vWorldPos);
    vec3 dy = dFdy(vWorldPos);
    vec3 normal = normalize(cross(dx, dy));
    if (dot(normal, normal) < 0.001)
        normal = vec3(0.0, 0.0, 1.0);
    vec3 lightDir = normalize(-uLightDir);
    float shadow = SampleCascadeShadow(vWorldPos, normal);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 ambient = uAmbientStrength * albedo.rgb * uAmbientColor;
    vec3 lit = ambient + diff * albedo.rgb * uLightColor * uLightIntensity * shadow;
    if (uFogMode != 0) {
        float dist = length(vViewPos.xyz);
        float fogFactor = exp(-uFogDensity * dist);
        if (uFogMode == 2) {
            float heightTerm = exp(-uFogHeightFalloff * max(vWorldPos.z - uFogHeight, 0.0));
            fogFactor = exp(-uFogDensity * dist * heightTerm);
        }
        lit = mix(uFogColor, lit, clamp(fogFactor, 0.0, 1.0));
    }
    FragColor = vec4(lit, albedo.a);
    float dz = max(abs(dFdx(gl_FragCoord.z)), abs(dFdy(gl_FragCoord.z)));
    gl_FragDepth = clamp(gl_FragCoord.z + uPolyFactor * dz + uPolyUnits / 16777216.0, 0.0, 1.0);
}";

        private readonly IRenderContext _renderContext;
        private ShaderProgram _terrainShader;
        private ShaderProgram _spriteShader;

        public TerrainRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
        }

        public void Initialize()
        {
            _terrainShader = new ShaderProgram(_renderContext, TerrainVertex, TerrainFragment);
            _spriteShader = new ShaderProgram(_renderContext, SpriteShader.VertexShaderSource, SpriteShader.FragmentShaderSource);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, uint terrainTextureId, VertexBuffer terrainBuffer, float[,] heightmap = null, bool drawWireframe = true)
        {
            RenderTerrain(view, projection, hasColorTexture, terrainTextureId, terrainBuffer, null, heightmap, drawWireframe);
        }

        public void RenderTerrain(Matrix4x4 view, Matrix4x4 projection, bool hasColorTexture, uint terrainTextureId, VertexBuffer terrainBuffer, VertexBuffer wireframeBuffer, float[,] heightmap, bool drawWireframe)
        {
            if (terrainBuffer == null && wireframeBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.DepthFunc(_renderContext.Enums.Less);
            _renderContext.ColorMask(true, true, true, true);
            _renderContext.Enable(_renderContext.Enums.CullFace);
            _renderContext.CullFace(_renderContext.Enums.Back);
            _renderContext.FrontFace(_renderContext.Enums.CounterClockwise);

            _terrainShader.Use();
            _terrainShader.SetMatrix4("uView", view);
            _terrainShader.SetMatrix4("uProjection", projection);
            _terrainShader.SetMatrix4("uModel", Matrix4x4.Identity);
            _terrainShader.SetUniform("uLightDir", LightingFrame.DefaultSunDirection.X, LightingFrame.DefaultSunDirection.Y, LightingFrame.DefaultSunDirection.Z);
            _terrainShader.SetUniform("uLightColor", 1f, 1f, 1f);
            _terrainShader.SetUniform("uLightIntensity", 1f);
            _terrainShader.SetUniform("uAmbientColor", 0.30f, 0.30f, 0.34f);
            _terrainShader.SetUniform("uAmbientStrength", 0.30f);
            LightingFrame.Current?.ApplyTo(_terrainShader, _renderContext);
            _terrainShader.SetUniform("uReceiveShadows", 1);

            bool textured = hasColorTexture && terrainTextureId != 0 && terrainBuffer != null;

            if (textured || (drawWireframe && terrainBuffer != null))
            {
                if (textured)
                {
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _terrainShader.SetUniform("uHasTexture", 1);
                    _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, terrainTextureId);
                    _terrainShader.SetUniform("uTexture", 0);
                }
                else
                {
                    _renderContext.ColorMask(false, false, false, false);
                    _terrainShader.SetUniform("uHasTexture", 0);
                }

                _terrainShader.SetUniform("uPolyFactor", 1f);
                _terrainShader.SetUniform("uPolyUnits", 2f);
                terrainBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, terrainBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.ColorMask(true, true, true, true);
                _renderContext.Enable(_renderContext.Enums.CullFace);
                _renderContext.CullFace(_renderContext.Enums.Back);
            }

            if (drawWireframe)
            {
                VertexBuffer lines = wireframeBuffer ?? terrainBuffer;
                if (lines != null && lines.GetIndexCount() > 0)
                {
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                    _renderContext.DepthMask(false);
                    _renderContext.DepthFunc(_renderContext.Enums.Less);
                    _renderContext.Enable(_renderContext.Enums.Blend);
                    _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
                    _renderContext.Enable(_renderContext.Enums.LineSmooth);
                    _renderContext.Disable(_renderContext.Enums.CullFace);
                    _renderContext.LineWidth(1.25f);

                    _terrainShader.SetUniform("uHasTexture", 0);
                    _terrainShader.SetUniform("uPolyFactor", 0f);
                    _terrainShader.SetUniform("uPolyUnits", 0f);
                    lines.Bind();
                    _renderContext.DrawElements(_renderContext.Enums.Lines, lines.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);

                    _renderContext.LineWidth(1f);
                    _renderContext.Disable(_renderContext.Enums.LineSmooth);
                    _renderContext.Disable(_renderContext.Enums.Blend);
                    _renderContext.Enable(_renderContext.Enums.CullFace);
                    _renderContext.DepthMask(true);
                    _renderContext.Enable(_renderContext.Enums.DepthTest);
                }
            }

            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        public void RenderGhost(ShaderProgram spriteShader, Matrix4x4 view, Matrix4x4 projection, Matrix4x4 ghostModel, uint ghostTextureId, VertexBuffer ghostBuffer, bool isPaintMode)
        {
            if (ghostBuffer == null) return;

            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);

            if (isPaintMode && ghostTextureId != 0)
            {
                spriteShader.Use();
                spriteShader.SetMatrix4("uModel", ghostModel);
                spriteShader.SetMatrix4("uView", view);
                spriteShader.SetMatrix4("uProjection", projection);
                _renderContext.ActiveTexture(_renderContext.Enums.Texture0);
                _renderContext.BindTexture(_renderContext.Enums.Texture2D, ghostTextureId);
                spriteShader.SetUniform("uTexture", 0);
                ghostBuffer.Bind();
                _renderContext.DrawElements(_renderContext.Enums.Triangles, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
            }
            else
            {
                _terrainShader.Use();
                _terrainShader.SetMatrix4("uModel", ghostModel);
                _terrainShader.SetUniform("uHasTexture", 0);
                _terrainShader.SetUniform("uPolyFactor", 0f);
                _terrainShader.SetUniform("uPolyUnits", 0f);
                ghostBuffer.Bind();
                _renderContext.Enable(_renderContext.Enums.LineSmooth);
                _renderContext.DrawElements(_renderContext.Enums.Lines, ghostBuffer.GetIndexCount(), _renderContext.Enums.UnsignedInt, null);
                _renderContext.Disable(_renderContext.Enums.LineSmooth);
            }

            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthMask(true);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }

        public void Dispose()
        {
            _terrainShader?.Dispose();
            _spriteShader?.Dispose();
        }
    }
}
