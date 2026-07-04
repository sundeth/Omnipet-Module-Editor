using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using OmnipetModuleEditor.Utils;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// Refreshes the player's GLOBAL sprite library (E:\Omnipet\assets\{monsters,
    /// monsters_dot, monsters_hidef}) against the Digimon Database for the names
    /// used by the open module's pets and enemies.
    ///
    /// For each name + format the module uses (primary + secondary), it asks the
    /// server's sprite-checksum endpoint for the canonical sheet's hash, computes
    /// the identical hash for the local asset file (md5("stem:size")), and — when
    /// they differ (or the local file is missing) — downloads the global sheet and
    /// overwrites the local one. The hash uses the server's canonical stem, so the
    /// comparison reduces to a content-size check exactly like the server's sync.
    /// </summary>
    public class LocalSpriteUpdater
    {
        // module sprite-format name -> (api fmt, assets sub-folder)
        private static readonly Dictionary<string, (string Fmt, string Folder)> FormatMap =
            new Dictionary<string, (string, string)>
            {
                { "Color", ("color", "monsters") },
                { "Dot",   ("dot",   "monsters_dot") },
                { "HD",    ("hd",    "monsters_hidef") },
            };

        private readonly string _modulePath;
        private readonly string _nameFormat;
        private readonly DigimonDb _db;
        private readonly List<(string Fmt, string Folder)> _formats;
        private readonly string _assetsDir;

        public LocalSpriteUpdater(string modulePath, string nameFormat,
            string primaryFormat, string secondaryFormat, DigimonDb db)
        {
            _modulePath = modulePath;
            _nameFormat = string.IsNullOrEmpty(nameFormat) ? SpriteUtils.DefaultNameFormat : nameFormat;
            _db = db;

            _formats = new List<(string, string)>();
            foreach (var f in new[] { primaryFormat, secondaryFormat })
            {
                var norm = SpriteUtils.NormalizeFormat(f);
                if (FormatMap.TryGetValue(norm, out var pair) && !_formats.Contains(pair))
                    _formats.Add(pair);
            }

            // assets live two levels above the module folder: <root>/modules/<MODULE> -> <root>/assets
            var gameRoot = Directory.GetParent(modulePath)?.Parent?.FullName;
            _assetsDir = gameRoot != null ? Path.Combine(gameRoot, "assets") : null;
        }

        public async Task<string> RunAsync(IProgress<(int Done, int Total, string Status)> progress)
        {
            if (_assetsDir == null)
                return "Could not locate the assets folder for this module.";
            if (_formats.Count == 0)
                return "The module has no sprite formats configured.";

            var names = GatherNames();
            int total = names.Count;
            int updated = 0, checkedCount = 0, skipped = 0;

            for (int i = 0; i < total; i++)
            {
                var name = names[i];
                progress?.Report((i, total, $"Checking {name} ({i + 1}/{total})"));

                var match = _db.Match(name);
                if (!match.IsMatch || string.IsNullOrEmpty(match.Record.NameEnglish))
                {
                    skipped++;
                    continue;
                }
                checkedCount++;
                string id = match.Record.Id;
                string serverStem = match.Record.NameEnglish.Replace(":", "_");

                Dictionary<string, DigimonDbClient.ChecksumEntry> serverSums;
                try
                {
                    serverSums = await DigimonDbClient.GetSpriteChecksumAsync(name, _formats.Select(f => f.Fmt));
                }
                catch
                {
                    continue;   // a transient checksum failure shouldn't abort the whole run
                }

                foreach (var (fmt, folder) in _formats)
                {
                    if (!serverSums.TryGetValue(fmt, out var entry) || string.IsNullOrEmpty(entry.Hash))
                        continue;   // server has no global sheet for this digimon + format

                    var localFile = Path.Combine(_assetsDir, folder,
                        SpriteUtils.GetSpriteName(name, _nameFormat) + ".zip");

                    string localHash = null;
                    if (File.Exists(localFile))
                    {
                        long size = new FileInfo(localFile).Length;
                        localHash = Md5Short($"{serverStem}:{size}");
                    }

                    if (localHash == entry.Hash)
                        continue;   // up to date

                    var bytes = await DigimonDbClient.DownloadGlobalSheetAsync(id, fmt);
                    if (bytes == null || bytes.Length == 0)
                        continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(localFile));
                    File.WriteAllBytes(localFile, bytes);
                    updated++;
                }
            }

            progress?.Report((total, total, "Done."));
            return $"Checked {checkedCount} matched name(s) ({skipped} skipped), "
                 + $"updated {updated} sprite sheet(s) in the assets library.";
        }

        /// <summary>Distinct, non-empty pet + enemy names from the module.</summary>
        private List<string> GatherNames()
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Collect(string file, string arrayKey)
            {
                var path = Path.Combine(_modulePath, file);
                if (!File.Exists(path)) return;
                try
                {
                    var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
                    if (root?[arrayKey] is JsonArray arr)
                        foreach (var node in arr)
                            if (node is JsonObject o && o.TryGetPropertyValue("name", out var n)
                                && n is JsonValue v && v.TryGetValue<string>(out var s)
                                && !string.IsNullOrWhiteSpace(s) && seen.Add(s))
                                names.Add(s);
                }
                catch { /* unreadable file -> no names from it */ }
            }

            Collect("monster.json", "monster");
            Collect("battle.json", "enemies");
            return names;
        }

        private static string Md5Short(string s)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString().Substring(0, 8);
            }
        }
    }
}
