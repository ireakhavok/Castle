using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SiegeEngine.Core.Managers
{
    public static class ExportGameContent
    {
        public static void Copy(string projectPath, string exportRoot, string hostBin)
        {
            if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(exportRoot))
                return;
            Directory.CreateDirectory(exportRoot);

            foreach (string folder in new[] { "Assets", "Textures", "Sounds", "Scenes", "Scripts" })
            {
                string src = Path.Combine(projectPath, folder);
                if (!Directory.Exists(src)) continue;
                CopyDir(src, Path.Combine(exportRoot, folder));
                Console.WriteLine("[Export] Copied project " + folder);
            }

            string hostSounds = Path.Combine(hostBin ?? "", "Assets", "Sounds");
            if (Directory.Exists(hostSounds))
            {
                string destSounds = Path.Combine(exportRoot, "Assets", "Sounds");
                Directory.CreateDirectory(destSounds);
                CopyDir(hostSounds, destSounds);
                Console.WriteLine("[Export] Copied engine Assets/Sounds");
            }

            CopyReferencedAudio(projectPath, exportRoot, hostBin);
        }

        static void CopyReferencedAudio(string projectPath, string exportRoot, string hostBin)
        {
            var texts = new System.Collections.Generic.List<string>();
            TryRead(texts, Path.Combine(projectPath, "project.json"));
            TryRead(texts, Path.Combine(exportRoot, "play_payload.json"));
            TryRead(texts, Path.Combine(Path.GetTempPath(), "castle_play_payload.json"));
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string text in texts)
            {
                foreach (Match m in Regex.Matches(text, @"Sounds\\[^""]+\.(?:wav|ogg|mp3)", RegexOptions.IgnoreCase))
                {
                    string rel = m.Value.Replace('/', Path.DirectorySeparatorChar);
                    if (!seen.Add(rel)) continue;
                    string fileName = Path.GetFileName(rel);
                    string[] sources =
                    {
                        Path.Combine(projectPath, rel),
                        Path.Combine(projectPath, "Assets", rel),
                        Path.Combine(hostBin ?? "", "Assets", rel),
                        Path.Combine(hostBin ?? "", "Assets", "Sounds", "IDE", "Music", fileName),
                        Path.Combine(hostBin ?? "", "Assets", "Sounds", fileName)
                    };
                    string dest = Path.Combine(exportRoot, "Assets", rel);
                    if (File.Exists(dest)) continue;
                    foreach (string src in sources)
                    {
                        if (!File.Exists(src)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        File.Copy(src, dest, true);
                        Console.WriteLine("[Export] Copied referenced audio " + rel);
                        break;
                    }
                }
            }
        }

        static void TryRead(System.Collections.Generic.List<string> texts, string path)
        {
            try { if (File.Exists(path)) texts.Add(File.ReadAllText(path)); } catch { }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(src, dir);
                Directory.CreateDirectory(Path.Combine(dst, rel));
            }
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(src, file);
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }
    }
}
