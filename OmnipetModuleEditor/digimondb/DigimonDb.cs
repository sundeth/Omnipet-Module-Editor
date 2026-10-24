using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// Outcome of a name match: the resolved record and which field matched
    /// ("name-english", "custom", "special", ...). Record is null when unmatched.
    /// </summary>
    public struct MatchResult
    {
        public DigimonRecord Record;
        public string Field;
        public bool IsMatch => Record != null;
    }

    /// <summary>
    /// In-memory Digimon Database + name matcher. Ports the matching logic of
    /// the Digimon Database's module_sync.py (normalisation, multi-field name
    /// index, persistent custom matches, and the context-aware special
    /// matchers for MetalGreymon / Whamon).
    /// </summary>
    public class DigimonDb
    {
        // Name fields consulted, in priority order (mirrors module_sync NAME_FIELDS).
        private static readonly (string Label, Func<DigimonRecord, IEnumerable<string>> Get)[] FieldOrder =
        {
            ("name-english",               r => One(r.NameEnglish)),
            ("name-dub",                    r => One(r.NameDub)),
            ("name-japanese",               r => One(r.NameJapanese)),
            ("name-japanese-romanization",  r => One(r.NameRomanization)),
            ("name-alternatives",           r => r.NameAlternatives ?? Enumerable.Empty<string>()),
        };

        private readonly List<DigimonRecord> _records;
        private readonly Dictionary<string, DigimonRecord> _byId = new Dictionary<string, DigimonRecord>();
        private readonly Dictionary<string, DigimonRecord> _nameIdx = new Dictionary<string, DigimonRecord>();
        private readonly Dictionary<string, DigimonRecord>[] _fieldIdx;
        private readonly Dictionary<string, DigimonRecord> _customMap = new Dictionary<string, DigimonRecord>();

        /// <summary>Loaded custom matches for display: (customName, dbName).</summary>
        public readonly List<(string Custom, string DbName)> CustomPairs = new List<(string, string)>();
        /// <summary>Custom matches whose target could not be resolved in the DB.</summary>
        public readonly List<(string Custom, string Target)> CustomUnresolved = new List<(string, string)>();

        public DigimonDb(List<DigimonRecord> records, string customMatchesPath)
        {
            _records = records ?? new List<DigimonRecord>();
            _fieldIdx = new Dictionary<string, DigimonRecord>[FieldOrder.Length];
            for (int i = 0; i < FieldOrder.Length; i++)
                _fieldIdx[i] = new Dictionary<string, DigimonRecord>();

            foreach (var rec in _records)
            {
                if (rec.Id != null && !_byId.ContainsKey(rec.Id))
                    _byId[rec.Id] = rec;

                for (int i = 0; i < FieldOrder.Length; i++)
                {
                    foreach (var v in FieldOrder[i].Get(rec))
                    {
                        var key = Norm(v);
                        if (key.Length == 0) continue;
                        if (!_fieldIdx[i].ContainsKey(key))
                            _fieldIdx[i][key] = rec;
                        if (!_nameIdx.ContainsKey(key))
                            _nameIdx[key] = rec;
                    }
                }
            }

            LoadCustomMatches(customMatchesPath);
        }

        // ----------------------------------------------------------------- norm

        /// <summary>
        /// Normalise a name for comparison: fold full-width brackets/colon, strip
        /// whitespace/-./':()_ and lowercase. Mirrors module_sync._norm.
        /// </summary>
        public static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace('（', '(').Replace('）', ')').Replace('：', ':');
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (char.IsWhiteSpace(c)) continue;
                switch (c)
                {
                    case '-': case '.': case '\'': case ':':
                    case '(': case ')': case '_':
                        continue;
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static IEnumerable<string> One(string v)
        {
            if (!string.IsNullOrEmpty(v)) yield return v;
        }

        // -------------------------------------------------------- custom matches

        private void LoadCustomMatches(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (!doc.RootElement.TryGetProperty("matches", out var matches)
                        || matches.ValueKind != JsonValueKind.Object)
                        return;
                    foreach (var entry in matches.EnumerateObject())
                    {
                        var custom = entry.Name;
                        var target = entry.Value.GetString() ?? "";
                        var rec = ResolveTarget(target);
                        if (rec == null)
                        {
                            CustomUnresolved.Add((custom, target));
                            continue;
                        }
                        var key = Norm(custom);
                        if (key.Length > 0)
                            _customMap[key] = rec;
                        CustomPairs.Add((custom, rec.DisplayName));
                    }
                }
            }
            catch { /* a malformed custom_matches.json simply yields no overrides */ }
        }

        /// <summary>Resolve a custom-match target: DB id first, else any name.</summary>
        public DigimonRecord ResolveTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (_byId.TryGetValue(target, out var byId)) return byId;
            return _nameIdx.TryGetValue(Norm(target), out var byName) ? byName : null;
        }

        // -------------------------------------------------------------- matching

        /// <summary>Match a bare name string (custom overrides, then name fields).</summary>
        public MatchResult Match(string name)
        {
            var key = Norm(name);
            if (key.Length == 0) return default(MatchResult);
            if (_customMap.TryGetValue(key, out var custom))
                return new MatchResult { Record = custom, Field = "custom" };
            for (int i = 0; i < _fieldIdx.Length; i++)
                if (_fieldIdx[i].TryGetValue(key, out var rec))
                    return new MatchResult { Record = rec, Field = FieldOrder[i].Label };
            return default(MatchResult);
        }

        /// <summary>
        /// Match a module entry, applying the context-aware special matchers
        /// (which look at attribute/stage) before the normal name match.
        /// </summary>
        public MatchResult MatchEntry(string name, string attribute, int? stage)
        {
            var key = Norm(name);
            string targetId = null;
            if (key == Norm("MetalGreymon"))
            {
                if (attribute == "Vi") targetId = "metalgreymon-web";   // MetalGreymon (Virus)
                else if (attribute == "Va") targetId = "metalgreymon-v"; // MetalGreymon (Vaccine)
            }
            else if (key == Norm("Whamon"))
            {
                if (stage == 4) targetId = "whamon_2";  // Whamon (Champion)
                else if (stage == 5) targetId = "whamon"; // Whamon (Perfect)
            }
            if (targetId != null && _byId.TryGetValue(targetId, out var special))
                return new MatchResult { Record = special, Field = "special" };
            return Match(name);
        }
    }
}
