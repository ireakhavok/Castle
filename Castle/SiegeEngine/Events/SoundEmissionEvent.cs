using SiegeEngine.Definitions;
using System.Numerics;
using System.Text.Json;

namespace SiegeEngine.Events
{
    public class SoundEmissionEvent : IEvent
    {
        public string Type => "SoundEmission";
        public SoundSource Source { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SoundEmissionEvent>(json);
            Source = obj.Source;
        }
    }

    public class SoundEvent : IEvent
    {
        public string Type => "Sound";
        public SoundSource Source { get; set; }
        public SoundRayTraceResult Result { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SoundEvent>(json);
            Source = obj.Source;
            Result = obj.Result;
        }
    }

    public class RayTraceResult
    {
        public bool DidHit { get; set; }
        public float Distance { get; set; }
        public Vector3 HitPoint { get; set; }
        public Vector3 HitNormal { get; set; }
        public MaterialProperties Material { get; set; }
        public float Intensity { get; set; }
        public float Delay { get; set; }
        public float LowPassCutoff { get; set; }
    }

    public class SoundRayTraceResult
    {
        public float Intensity { get; set; }
        public float Delay { get; set; }
        public float LowPassCutoff { get; set; }

        public byte[] Serialize()
        {
            var json = JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var obj = JsonSerializer.Deserialize<SoundRayTraceResult>(json);
            Intensity = obj.Intensity;
            Delay = obj.Delay;
            LowPassCutoff = obj.LowPassCutoff;
        }
    }
}