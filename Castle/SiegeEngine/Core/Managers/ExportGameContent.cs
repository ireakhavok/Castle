using System;
using System.IO;

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
