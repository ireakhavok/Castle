// Folder: SiegeEngine/Core/Definitions
// File: RuntimeSettings.cs
using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using SiegeEngine.Core.Physics;

namespace SiegeEngine.Core.Definitions
{
    public sealed class RuntimeSettings
    {
        public static RuntimeSettings Current { get; } = new RuntimeSettings();
        public static RuntimeSettings Draft => Current;

        public const string ModeSinglePlayer = "Single Player";
        public const string ModeMultiplayer = "Multiplayer";
        public const string ModePeerToPeer = "Peer to Peer";
        public const string ModeHosted = "Hosted";

        public static readonly string[] Modes =
        {
            ModeSinglePlayer,
            ModeMultiplayer,
            ModePeerToPeer,
            ModeHosted
        };

        public bool UseFixedTimestep { get; set; } = false;
        public float StepRateHz { get; set; } = 60f;
        public float GravityZ { get; set; } = -9.81f;
        public float FrameCapHz { get; set; } = 0f;
        public float MouseSensitivityX { get; set; } = 0.02f;
        public float MouseSensitivityY { get; set; } = 0.02f;
        public string Mode { get; set; } = ModeSinglePlayer;
        public bool LookLive { get; set; } = true;

        public const float LookSliderMin = 0f;
        public const float LookSliderMax = 100f;
        public const float LookSliderScale = 1000f;
        public const float LookSliderDefault = 20f;

        public static float SliderToLook(float slider)
        {
            if (slider < LookSliderMin) slider = LookSliderMin;
            if (slider > LookSliderMax) slider = LookSliderMax;
            return slider / LookSliderScale;
        }

        public static float LookToSlider(float look)
        {
            float slider = look * LookSliderScale;
            if (slider < LookSliderMin) slider = LookSliderMin;
            if (slider > LookSliderMax) slider = LookSliderMax;
            return slider;
        }

        public void ApplyTo(PhysicsWorld world)
        {
            if (world == null) return;
            world.UseFixedTimestep = UseFixedTimestep && StepRateHz > 0f;
            if (StepRateHz > 0f)
                world.FixedTimestep = 1f / StepRateHz;
            Vector3 g = world.Gravity;
            world.Gravity = new Vector3(g.X, g.Y, GravityZ);
        }

        public void CopyFrom(RuntimeSettings src)
        {
            if (src == null) return;
            UseFixedTimestep = src.UseFixedTimestep;
            StepRateHz = src.StepRateHz;
            GravityZ = src.GravityZ;
            FrameCapHz = src.FrameCapHz;
            MouseSensitivityX = src.MouseSensitivityX;
            MouseSensitivityY = src.MouseSensitivityY;
            if (!string.IsNullOrEmpty(src.Mode))
                Mode = src.Mode;
        }

        public static void ReplaceCurrent(RuntimeSettings src)
        {
            Current.CopyFrom(src);
        }

        public bool ShouldPresent(ref long lastPresentTimestamp)
        {
            if (FrameCapHz <= 0f) return true;
            long now = Stopwatch.GetTimestamp();
            long interval = (long)(Stopwatch.Frequency / (double)FrameCapHz);
            if (interval <= 0) return true;
            if (lastPresentTimestamp == 0)
            {
                lastPresentTimestamp = now;
                return true;
            }
            if (now - lastPresentTimestamp < interval)
                return false;
            lastPresentTimestamp += interval;
            if (now - lastPresentTimestamp >= interval)
                lastPresentTimestamp = now;
            return true;
        }

        public float LookX
        {
            get { return MouseSensitivityX >= 0f ? MouseSensitivityX : 0.02f; }
        }

        public float LookY
        {
            get { return MouseSensitivityY >= 0f ? MouseSensitivityY : 0.02f; }
        }

        public static void ApplyMouseLook(ref float yaw, ref float pitch, float deltaX, float deltaY)
        {
            var s = Current;
            yaw += deltaX * s.LookX;
            pitch -= deltaY * s.LookY;
            if (pitch > 89f) pitch = 89f;
            if (pitch < -89f) pitch = -89f;
        }

        public static string NormalizeMode(string mode)
        {
            if (string.Equals(mode, "Server", StringComparison.OrdinalIgnoreCase))
                return ModeHosted;
            if (string.IsNullOrWhiteSpace(mode))
                return ModeSinglePlayer;
            for (int i = 0; i < Modes.Length; i++)
            {
                if (string.Equals(mode, Modes[i], StringComparison.OrdinalIgnoreCase))
                    return Modes[i];
            }
            return ModeSinglePlayer;
        }

        public static void ApplyFromPayloadRoot(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object) return;
            JsonElement runtimeElem;
            if (!TryGetIgnoreCase(root, "Runtime", out runtimeElem)) return;
            ApplyFromRuntimeElement(runtimeElem);
        }

        public static bool TryLoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return false;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(path)))
                {
                    JsonElement root = doc.RootElement;
                    JsonElement runtimeElem;
                    if (TryGetIgnoreCase(root, "Runtime", out runtimeElem))
                        ApplyFromRuntimeElement(runtimeElem);
                    else
                        ApplyFromRuntimeElement(root);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyFromRuntimeElement(JsonElement runtimeElem)
        {
            if (runtimeElem.ValueKind != JsonValueKind.Object) return;
            var loaded = new RuntimeSettings();
            loaded.CopyFrom(Current);
            bool useFixed;
            if (TryGetIgnoreCase(runtimeElem, "UseFixedTimestep", out JsonElement fixedEl) && TryGetBool(fixedEl, out useFixed))
                loaded.UseFixedTimestep = useFixed;
            float step;
            if (TryGetIgnoreCase(runtimeElem, "StepRateHz", out JsonElement stepEl) && TryGetFloat(stepEl, out step))
                loaded.StepRateHz = step;
            float gravity;
            if (TryGetIgnoreCase(runtimeElem, "GravityZ", out JsonElement gravityEl) && TryGetFloat(gravityEl, out gravity))
                loaded.GravityZ = gravity;
            float cap;
            if (TryGetIgnoreCase(runtimeElem, "FrameCapHz", out JsonElement capEl) && TryGetFloat(capEl, out cap))
                loaded.FrameCapHz = cap;
            float mouseX;
            if (TryGetIgnoreCase(runtimeElem, "MouseSensitivityX", out JsonElement mouseXEl) && TryGetFloat(mouseXEl, out mouseX))
                loaded.MouseSensitivityX = mouseX;
            float mouseY;
            if (TryGetIgnoreCase(runtimeElem, "MouseSensitivityY", out JsonElement mouseYEl) && TryGetFloat(mouseYEl, out mouseY))
                loaded.MouseSensitivityY = mouseY;
            if (TryGetIgnoreCase(runtimeElem, "Mode", out JsonElement modeEl) && modeEl.ValueKind == JsonValueKind.String)
                loaded.Mode = NormalizeMode(modeEl.GetString());
            ReplaceCurrent(loaded);
        }

        private static bool TryGetIgnoreCase(JsonElement obj, string name, out JsonElement value)
        {
            value = default;
            if (obj.ValueKind != JsonValueKind.Object) return false;
            if (obj.TryGetProperty(name, out value)) return true;
            foreach (JsonProperty prop in obj.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            return false;
        }

        private static bool TryGetFloat(JsonElement el, out float value)
        {
            value = 0f;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetSingle(out value))
                return true;
            if (el.ValueKind == JsonValueKind.String &&
                float.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            return false;
        }

        private static bool TryGetBool(JsonElement el, out bool value)
        {
            value = false;
            if (el.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (el.ValueKind == JsonValueKind.False) { value = false; return true; }
            if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out value))
                return true;
            return false;
        }
    }
}
