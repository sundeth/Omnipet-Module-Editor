using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// A single Digimon record from the public Digimon Database API
    /// (https://digimon-db.omnipet.app.br/api). Only the fields needed for
    /// name-matching and min-weight import are mapped.
    /// </summary>
    public class DigimonRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("name_english")] public string NameEnglish { get; set; }
        [JsonPropertyName("name_japanese")] public string NameJapanese { get; set; }
        [JsonPropertyName("name_romanization")] public string NameRomanization { get; set; }
        [JsonPropertyName("name_dub")] public string NameDub { get; set; }
        [JsonPropertyName("name_alternatives")] public List<string> NameAlternatives { get; set; }
        [JsonPropertyName("min_weight")] public int? MinWeight { get; set; }

        // Resolved metadata names + per-module extra-data (used by the Add-pet flow).
        [JsonPropertyName("levels")] public List<string> Levels { get; set; }
        [JsonPropertyName("attributes")] public List<string> Attributes { get; set; }
        [JsonPropertyName("extra_data")] public Dictionary<string, JsonElement> ExtraData { get; set; }

        /// <summary>Best human-readable label for the record.</summary>
        public string DisplayName => NameEnglish ?? NameDub ?? Id;
    }

    internal class LevelEntry
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name_english")] public string NameEnglish { get; set; }
    }

    internal class SearchResults
    {
        [JsonPropertyName("total")] public int Total { get; set; }
        [JsonPropertyName("offset")] public int Offset { get; set; }
        [JsonPropertyName("limit")] public int Limit { get; set; }
        [JsonPropertyName("results")] public List<DigimonRecord> Results { get; set; }
    }

    /// <summary>
    /// Fetches the full Digimon Database catalogue from the live API. The
    /// result is cached for the lifetime of the process so repeated menu
    /// actions don't re-download all ~1500 records.
    /// </summary>
    public static class DigimonDbClient
    {
        public const string BaseUrl = "https://digimon-db.omnipet.app.br";
        private const int PageSize = 216; // server MAX_PAGE_SIZE

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        private static List<DigimonRecord> _cache;

        /// <summary>
        /// Returns every Digimon record, downloading and caching on first use.
        /// Throws on network/parse failure so callers can surface the error.
        /// </summary>
        public static async Task<List<DigimonRecord>> GetAllAsync()
        {
            if (_cache != null)
                return _cache;

            var all = new List<DigimonRecord>();
            int offset = 0;
            int total = int.MaxValue;
            while (offset < total)
            {
                var url = $"{BaseUrl}/api/digimon?offset={offset}&limit={PageSize}";
                var body = await _http.GetStringAsync(url);
                var page = JsonSerializer.Deserialize<SearchResults>(body, _json);
                if (page == null || page.Results == null || page.Results.Count == 0)
                    break;
                all.AddRange(page.Results);
                total = page.Total;
                offset += page.Results.Count;
            }

            _cache = all;
            return _cache;
        }

        /// <summary>Discard the cached catalogue so the next call re-downloads.</summary>
        public static void ClearCache()
        {
            _cache = null;
            _levelNameToId = null;
        }

        private static Dictionary<string, int> _levelNameToId;

        /// <summary>
        /// Level name -> id map (cached). A module "stage" equals the DB level
        /// id, so this is used to group catalogue records by stage.
        /// </summary>
        public static async Task<Dictionary<string, int>> GetLevelNameToIdAsync()
        {
            if (_levelNameToId != null)
                return _levelNameToId;
            var body = await _http.GetStringAsync($"{BaseUrl}/api/levels");
            var levels = JsonSerializer.Deserialize<List<LevelEntry>>(body, _json) ?? new List<LevelEntry>();
            var map = new Dictionary<string, int>();
            foreach (var l in levels)
                if (!string.IsNullOrEmpty(l.NameEnglish))
                    map[l.NameEnglish] = l.Id;
            _levelNameToId = map;
            return _levelNameToId;
        }

        /// <summary>
        /// Download a module-scoped sprite-sheet zip for a Digimon, or null when
        /// the module has no custom sheet for that format (HTTP 404).
        /// </summary>
        public static async Task<byte[]> DownloadModuleSheetAsync(string id, string fmt, string module)
        {
            try
            {
                var url = $"{BaseUrl}/api/digimon/{Uri.EscapeDataString(id)}/sheet/{fmt}"
                          + $"?module={Uri.EscapeDataString(module)}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                    return null;
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Download the global sprite-sheet zip for a Digimon, or null when the
        /// global sheet for that format doesn't exist (HTTP 404).
        /// </summary>
        public static async Task<byte[]> DownloadGlobalSheetAsync(string id, string fmt)
        {
            try
            {
                var url = $"{BaseUrl}/api/digimon/{Uri.EscapeDataString(id)}/sheet/{fmt}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                    return null;
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>One format's entry from the sprite-checksum endpoint.</summary>
        public class ChecksumEntry
        {
            public string Hash { get; set; }
            public int Count { get; set; }
        }

        /// <summary>
        /// Query the per-format sprite checksum for a single name. Returns a map
        /// of format -> entry; formats the server has no sprites for are absent.
        /// </summary>
        public static async Task<Dictionary<string, ChecksumEntry>> GetSpriteChecksumAsync(
            string name, IEnumerable<string> fmts)
        {
            var sb = new StringBuilder($"{BaseUrl}/api/sprites/checksum?names={Uri.EscapeDataString(name)}");
            foreach (var f in fmts)
                sb.Append("&fmt=").Append(Uri.EscapeDataString(f));

            var result = new Dictionary<string, ChecksumEntry>();
            var body = await _http.GetStringAsync(sb.ToString());
            using (var doc = JsonDocument.Parse(body))
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object)
                        continue;   // null = server has no sprites for that format
                    string hash = prop.Value.TryGetProperty("hash", out var h) ? h.GetString() : null;
                    int count = prop.Value.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                    result[prop.Name] = new ChecksumEntry { Hash = hash, Count = count };
                }
            }
            return result;
        }
    }
}
