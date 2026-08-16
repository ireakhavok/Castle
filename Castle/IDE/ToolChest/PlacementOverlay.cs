using Keystone;
using SiegeEngine.Core.Rendering.ContextManagement;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Managers;
using System;
using System.Numerics;
using SiegeEngine.Core.Rendering.Renderers;
namespace ToolChest
{
    public class PlacementOverlay : ICustomOverlay
    {
        private readonly EventBus _eventBus;
        private Vector3 _camPos = Vector3.Zero;
        private float _camYaw = 0f;
        private float _camPitch = -30f;
        public PlacementOverlay(EventBus eventBus)
        {
            _eventBus = eventBus;
        }
        public void SetCameraState(Vector3 pos, float yaw, float pitch)
        {
            _camPos = pos;
            _camYaw = yaw;
            _camPitch = pitch;
        }
        public void Draw(UIQuadRenderer quadRenderer, float panelWidth, float panelHeight)
        {
            float alpha = 0.35f;
            quadRenderer.DrawQuad(0, 0, 12f, panelHeight, new Vector4(0.3f, 0.6f, 0.95f, alpha), panelWidth, panelHeight);
        }
        public void PerformPlacementRaycast(Vector2 relMouse, float contentW, float contentH)
        {
            var level = ProjectSettings.Current.CurrentLevel;
            var heightmap = ProjectSettings.Current.CurrentHeightmap;
            if (heightmap == null || level == null) return;
            float scaleX = level.Terrain?.WorldScaleX ?? 1f;
            float scaleZ = level.Terrain?.WorldScaleZ ?? 1f;
            int w = heightmap.GetLength(0);
            int h = heightmap.GetLength(1);
            Vector3 rayOrigin = _camPos;
            if (rayOrigin == Vector3.Zero) rayOrigin = new Vector3(w * scaleX * 0.5f, h * scaleZ * 0.5f, 500f);
            float yawRad = _camYaw * (MathF.PI / 180f);
            float pitchRad = _camPitch * (MathF.PI / 180f);
            Vector3 rayDir = Vector3.Normalize(new Vector3(
                MathF.Cos(pitchRad) * MathF.Sin(yawRad),
                MathF.Cos(pitchRad) * MathF.Cos(yawRad),
                MathF.Sin(pitchRad)));
            const float maxDist = 10000f;
            const float step = 1f;
            Vector3 hitPoint = Vector3.Zero;
            bool hit = false;
            for (float t = 0; t < maxDist; t += step)
            {
                Vector3 p = rayOrigin + rayDir * t;
                int ix = (int)Math.Clamp(p.X / scaleX, 0, w - 1);
                int iy = (int)Math.Clamp(p.Y / scaleZ, 0, h - 1);
                float terrainZ = heightmap[ix, iy];
                if (p.Z <= terrainZ)
                {
                    float tLow = t - step;
                    float tHigh = t;
                    for (int i = 0; i < 10; i++)
                    {
                        float tMid = (tLow + tHigh) / 2f;
                        p = rayOrigin + rayDir * tMid;
                        ix = (int)Math.Clamp(p.X / scaleX, 0, w - 1);
                        iy = (int)Math.Clamp(p.Y / scaleZ, 0, h - 1);
                        terrainZ = heightmap[ix, iy];
                        if (p.Z <= terrainZ) tHigh = tMid;
                        else tLow = tMid;
                    }
                    hitPoint = rayOrigin + rayDir * tHigh;
                    hit = true;
                    break;
                }
            }
            if (hit)
            {
                var evt = new EntityPlacedEvent(0, "AssetPack", hitPoint);
                _eventBus.Publish(evt);
            }
        }
    }
}