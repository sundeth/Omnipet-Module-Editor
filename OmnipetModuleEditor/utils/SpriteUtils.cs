using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OmnipetModuleEditor.Utils
{
    /// <summary>
    /// Sprite loading utilities for pets and enemies with multi-format fallback support.
    /// Supports Color, Dot, and HD sprite formats with configurable priority order.
    /// </summary>
    public static class SpriteUtils
    {
        public const string DefaultNameFormat = "$_dmc";

        /// <summary>
        /// Maps a sprite format name to its folder name.
        /// </summary>
        private static readonly Dictionary<string, string> FormatFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Color", "monsters" },
            { "Dot", "monsters_dot" },
            { "HD", "monsters_hidef" }
        };

        /// <summary>
        /// All available sprite format names.
        /// </summary>
        public static readonly string[] AllFormats = { "Color", "Dot", "HD" };

        /// <summary>
        /// Result of a sprite load operation, including which format was found.
        /// </summary>
        public class SpriteLoadResult
        {
            public Dictionary<string, Image> Sprites { get; set; } = new Dictionary<string, Image>();
            /// <summary>The format that was successfully loaded (e.g. "Color", "Dot", "HD"), or null if nothing found.</summary>
            public string LoadedFormat { get; set; }
            /// <summary>The full path (directory or zip) from which sprites were loaded, or null.</summary>
            public string LoadedPath { get; set; }

            public bool HasSprites => Sprites.Count > 0;
        }

        /// <summary>
        /// Generate standardized sprite folder/zip name using module name_format.
        /// </summary>
        public static string GetSpriteName(string petName, string nameFormat = DefaultNameFormat)
        {
            if (string.IsNullOrEmpty(petName)) return "";
            if (string.IsNullOrEmpty(nameFormat)) nameFormat = DefaultNameFormat;
            return nameFormat.Replace("$", petName).Replace(":", "_");
        }

        /// <summary>
        /// Builds the ordered list of sprite formats to try: primary, secondary, then the remaining one.
        /// </summary>
        public static string[] GetFormatOrder(string primary, string secondary)
        {
            primary = NormalizeFormat(primary);
            secondary = NormalizeFormat(secondary);
            if (primary == secondary) secondary = null;

            var order = new List<string>(3);
            order.Add(primary);
            if (secondary != null) order.Add(secondary);
            foreach (var f in AllFormats)
                if (!order.Contains(f)) order.Add(f);
            return order.ToArray();
        }

        /// <summary>
        /// Normalizes a format string. Accepts display names like "Color (Default)" and maps to "Color".
        /// </summary>
        public static string NormalizeFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return "Color";
            if (format.StartsWith("Color", StringComparison.OrdinalIgnoreCase)) return "Color";
            if (format.Equals("Dot", StringComparison.OrdinalIgnoreCase)) return "Dot";
            if (format.Equals("HD", StringComparison.OrdinalIgnoreCase)) return "HD";
            return "Color";
        }

        /// <summary>
        /// Returns the monsters folder name for a given format.
        /// </summary>
        public static string GetFolderForFormat(string format)
        {
            format = NormalizeFormat(format);
            return FormatFolders.TryGetValue(format, out var folder) ? folder : "monsters";
        }

        /// <summary>
        /// Loads sprites for a pet/enemy trying formats in priority order.
        /// For each format, tries: module dir ? module zip ? assets dir ? assets zip.
        /// </summary>
        public static SpriteLoadResult LoadSprites(string petName, string modulePath, string nameFormat,
            int maxFrames, string primaryFormat, string secondaryFormat)
        {
            if (string.IsNullOrEmpty(petName) || string.IsNullOrEmpty(modulePath))
                return new SpriteLoadResult();

            var spriteName = GetSpriteName(petName, nameFormat);
            var formatOrder = GetFormatOrder(primaryFormat, secondaryFormat);
            var gameRoot = Directory.GetParent(modulePath)?.Parent?.FullName;

            foreach (var format in formatOrder)
            {
                var folder = GetFolderForFormat(format);
                var result = TryLoadFromFolder(spriteName, modulePath, gameRoot, folder, maxFrames);
                if (result.HasSprites)
                {
                    result.LoadedFormat = format;
                    return result;
                }
            }

            return new SpriteLoadResult();
        }

        /// <summary>
        /// Loads sprites trying module dir, module zip, assets dir, assets zip for a specific folder.
        /// </summary>
        private static SpriteLoadResult TryLoadFromFolder(string spriteName, string modulePath, string gameRoot, string folder, int maxFrames)
        {
            // Module directory
            var dir = Path.Combine(modulePath, folder, spriteName);
            var sprites = LoadSpritesFromDirectory(dir, maxFrames);
            if (sprites.Count > 0)
                return new SpriteLoadResult { Sprites = sprites, LoadedPath = dir };

            // Module zip
            var zip = Path.Combine(modulePath, folder, $"{spriteName}.zip");
            sprites = LoadSpritesFromZip(zip, maxFrames);
            if (sprites.Count > 0)
                return new SpriteLoadResult { Sprites = sprites, LoadedPath = zip };

            // Assets directory
            if (!string.IsNullOrEmpty(gameRoot))
            {
                dir = Path.Combine(gameRoot, "assets", folder, spriteName);
                sprites = LoadSpritesFromDirectory(dir, maxFrames);
                if (sprites.Count > 0)
                    return new SpriteLoadResult { Sprites = sprites, LoadedPath = dir };

                // Assets zip
                zip = Path.Combine(gameRoot, "assets", folder, $"{spriteName}.zip");
                sprites = LoadSpritesFromZip(zip, maxFrames);
                if (sprites.Count > 0)
                    return new SpriteLoadResult { Sprites = sprites, LoadedPath = zip };
            }

            return new SpriteLoadResult();
        }

        /// <summary>
        /// Finds the path (dir or zip) where sprites exist for a given pet, using the format priority order.
        /// Returns null if not found. Used to determine where to write new sprites (e.g. portrait upload).
        /// </summary>
        public static SpriteLoadResult FindSpriteLocation(string petName, string modulePath, string nameFormat,
            string primaryFormat, string secondaryFormat)
        {
            if (string.IsNullOrEmpty(petName) || string.IsNullOrEmpty(modulePath))
                return new SpriteLoadResult();

            var spriteName = GetSpriteName(petName, nameFormat);
            var formatOrder = GetFormatOrder(primaryFormat, secondaryFormat);
            var gameRoot = Directory.GetParent(modulePath)?.Parent?.FullName;

            foreach (var format in formatOrder)
            {
                var folder = GetFolderForFormat(format);
                var path = FindFirstExistingPath(spriteName, modulePath, gameRoot, folder);
                if (path != null)
                    return new SpriteLoadResult { LoadedFormat = format, LoadedPath = path };
            }

            return new SpriteLoadResult();
        }

        private static string FindFirstExistingPath(string spriteName, string modulePath, string gameRoot, string folder)
        {
            var dir = Path.Combine(modulePath, folder, spriteName);
            if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return dir;

            var zip = Path.Combine(modulePath, folder, $"{spriteName}.zip");
            if (File.Exists(zip)) return zip;

            if (!string.IsNullOrEmpty(gameRoot))
            {
                dir = Path.Combine(gameRoot, "assets", folder, spriteName);
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return dir;

                zip = Path.Combine(gameRoot, "assets", folder, $"{spriteName}.zip");
                if (File.Exists(zip)) return zip;
            }

            return null;
        }

        // --- Convenience methods used by existing code ---

        public static Dictionary<string, Image> LoadPetSprites(string petName, string modulePath,
            string nameFormat = DefaultNameFormat, int maxFrames = 20,
            string primaryFormat = "Color", string secondaryFormat = "HD")
        {
            return LoadSprites(petName, modulePath, nameFormat, maxFrames, primaryFormat, secondaryFormat).Sprites;
        }

        public static Image LoadSingleSprite(string petName, string modulePath,
            string nameFormat = DefaultNameFormat,
            string primaryFormat = "Color", string secondaryFormat = "HD")
        {
            var sprites = LoadSprites(petName, modulePath, nameFormat, 1, primaryFormat, secondaryFormat).Sprites;
            return sprites.ContainsKey("0") ? sprites["0"] : null;
        }

        /// <summary>
        /// Convert sprite dictionary to ordered list for compatibility with existing code.
        /// </summary>
        public static List<Image> ConvertSpritesToList(Dictionary<string, Image> spritesDict, int maxFrames = 20)
        {
            var spriteList = new List<Image>();
            for (int i = 0; i < maxFrames; i++)
            {
                spriteList.Add(spritesDict.ContainsKey(i.ToString()) ? spritesDict[i.ToString()] : null);
            }
            return spriteList;
        }

        // --- Low-level loaders ---

        private static Dictionary<string, Image> LoadSpritesFromDirectory(string spritePath, int maxFrames)
        {
            var sprites = new Dictionary<string, Image>();
            if (!Directory.Exists(spritePath)) return sprites;

            try
            {
                foreach (var filePath in Directory.GetFiles(spritePath, "*.png"))
                {
                    var name = Path.GetFileNameWithoutExtension(filePath);
                    if (int.TryParse(name, out var frameNum) && frameNum < maxFrames)
                    {
                        try
                        {
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                sprites[name] = new Bitmap(Image.FromStream(fs));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return sprites;
        }

        private static Dictionary<string, Image> LoadSpritesFromZip(string zipPath, int maxFrames)
        {
            var sprites = new Dictionary<string, Image>();
            if (!File.Exists(zipPath)) return sprites;

            try
            {
                using (var zipFile = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
                {
                    foreach (var entry in zipFile.Entries)
                    {
                        if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                        var name = Path.GetFileNameWithoutExtension(entry.Name);
                        if (!int.TryParse(name, out var frameNum) || frameNum >= maxFrames) continue;

                        try
                        {
                            using (var entryStream = entry.Open())
                            using (var ms = new MemoryStream())
                            {
                                entryStream.CopyTo(ms);
                                ms.Position = 0;
                                sprites[name] = new Bitmap(Image.FromStream(ms));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return sprites;
        }
    }
}