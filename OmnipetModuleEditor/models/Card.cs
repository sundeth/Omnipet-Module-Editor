using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    /// <summary>
    /// A collectable card in the module's collection (cards.json).
    /// Identity is the uuid <see cref="Id"/> minted by the Digimon Database
    /// card maker (or by the editor for Custom/legacy cards) and shared across
    /// modules. Mechanically only <see cref="Value"/> (+ L/R for 5-bit cards)
    /// matters; everything else is presentation.
    /// </summary>
    public class CollectionCard
    {
        public const string TypeSoulPlate = "Soul Plate";
        public const string TypeDdpChip = "DDP Chip";
        public const string TypeIdPlate = "iD Plate";
        public const string TypeCustom = "Custom";

        public static readonly string[] AllTypes =
            { TypeSoulPlate, TypeDdpChip, TypeIdPlate, TypeCustom };

        public static readonly string[] Rarities = { "Common", "Rare", "Legendary" };

        /// <summary>Relative pack-odds weight for each rarity (bulk import default).</summary>
        public static int RarityWeight(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return 5;
                case "Rare": return 25;
                default: return 70;
            }
        }

        /// <summary>Template-native sprite size per card type (the physical card size).</summary>
        public static System.Drawing.Size SpriteSize(string type)
        {
            switch (type)
            {
                case TypeSoulPlate: return new System.Drawing.Size(125, 275);
                case TypeIdPlate: return new System.Drawing.Size(205, 325);
                default: return new System.Drawing.Size(250, 350);   // DDP Chip / Custom max
            }
        }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("series")]
        public string Series { get; set; }

        /// <summary>Binary value, 1-10 bits ("01011").</summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>"L", "R" or "L/R". Soul Plates are always "L/R"; null for DDP/Custom.</summary>
        [JsonPropertyName("lr")]
        public string Lr { get; set; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "Common";

        /// <summary>True when the artwork was a custom upload — Edit Art unavailable.</summary>
        [JsonPropertyName("custom_art")]
        public bool CustomArt { get; set; }

        [JsonPropertyName("sprites")]
        public CardSprites Sprites { get; set; } = new CardSprites();

        /// <summary>The verbatim card-maker JSON (round-tripped for Edit Art).</summary>
        [JsonPropertyName("art")]
        public JsonElement? Art { get; set; }

        [JsonIgnore]
        public string DisplayLabel =>
            Type == TypeSoulPlate ? (Name ?? "") : $"{Name} (#{Number})";

        /// <summary>The RFID payload value string ("01011", "01011-L", "01011-R").</summary>
        [JsonIgnore]
        public string RfidValue =>
            (Lr == "L" || Lr == "R") ? $"{Value}-{Lr}" : Value;

        /// <summary>Builds the small JSON written onto physical NFID tags.</summary>
        public string BuildRfidJson()
        {
            var payload = new Dictionary<string, object>
            {
                ["id"] = Id,
                ["name"] = Name ?? "",
                ["value"] = RfidValue,
                ["number"] = Number,
            };
            if (int.TryParse(Series, out int s)) payload["series"] = s;
            else if (!string.IsNullOrEmpty(Series)) payload["series"] = Series;
            return JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public class CardSprites
    {
        [JsonPropertyName("front")]
        public string Front { get; set; }

        [JsonPropertyName("back")]
        public string Back { get; set; }
    }

    /// <summary>
    /// One entry of the Effects list: a binary value (+ optional L/R scope for
    /// 5-bit values) mapped to zero or more effects. Cards and effects are
    /// intentionally decoupled — foreign cards with a matching value trigger
    /// the effect even if no card in this module carries it.
    /// </summary>
    public class CardEffectGroup
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>"Any" (default), "L" or "R" — only meaningful for 5-bit values.</summary>
        [JsonPropertyName("lr")]
        public string Lr { get; set; } = "Any";

        [JsonPropertyName("effects")]
        public List<CardEffect> Effects { get; set; } = new List<CardEffect>();

        [JsonIgnore]
        public string DisplayLabel =>
            (Lr == "L" || Lr == "R") ? $"{Value}-{Lr}" : Value;
    }

    public class CardEffect
    {
        public static readonly string[] Types = { "Item", "DNA", "Encounter", "Unlock" };
        public static readonly string[] DnaOptions =
            { "X of each", "Beast", "Bird", "Machine", "Water", "Dragon", "Insect", "Holy", "Dark" };

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Item";

        // Item
        [JsonPropertyName("item")]
        public string Item { get; set; }

        // DNA
        [JsonPropertyName("dna")]
        public string Dna { get; set; }

        // Item / DNA
        [JsonPropertyName("amount")]
        public int? Amount { get; set; }

        // Encounter
        [JsonPropertyName("area")]
        public int? Area { get; set; }

        [JsonPropertyName("round")]
        public int? Round { get; set; }

        // Unlock
        [JsonPropertyName("unlock")]
        public string Unlock { get; set; }

        /// <summary>Pet version this effect applies to; -1 = all versions.</summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = -1;

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                string desc;
                switch (Type)
                {
                    case "Item": desc = $"Item: {Item} x{Amount ?? 1}"; break;
                    case "DNA": desc = $"DNA: {Dna} x{Amount ?? 1}"; break;
                    case "Encounter": desc = $"Encounter: area {Area ?? 0}, round {Round ?? 0}"; break;
                    case "Unlock": desc = $"Unlock: {Unlock}"; break;
                    default: desc = Type ?? "?"; break;
                }
                return Version >= 0 ? $"{desc} (v{Version})" : desc;
            }
        }
    }

    public class CardPack
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("sprite")]
        public string Sprite { get; set; }

        [JsonPropertyName("cards_per_pack")]
        public int CardsPerPack { get; set; } = 5;

        /// <summary>Holographic pull chance, percent with 2 decimals (e.g. 1.45).</summary>
        [JsonPropertyName("shine_chance")]
        public float ShineChance { get; set; }

        [JsonPropertyName("cards")]
        public List<PackEntry> Cards { get; set; } = new List<PackEntry>();
    }

    public class PackEntry
    {
        /// <summary>Card uuid.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Relative drop weight.</summary>
        [JsonPropertyName("odds")]
        public int Odds { get; set; } = 1;
    }

    /// <summary>In-memory representation of the module's cards.json.</summary>
    public class CollectionFile
    {
        public const string FileName = "cards.json";
        public const string SpritesFolder = "cards";
        public const string DdpSharedBack = "DDP Chip_back.png";

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("cards")]
        public List<CollectionCard> Cards { get; set; } = new List<CollectionCard>();

        [JsonPropertyName("effects")]
        public List<CardEffectGroup> Effects { get; set; } = new List<CardEffectGroup>();

        [JsonPropertyName("packs")]
        public List<CardPack> Packs { get; set; } = new List<CardPack>();

        [JsonIgnore]
        public bool IsEmpty => Cards.Count == 0 && Effects.Count == 0 && Packs.Count == 0;

        public static CollectionFile Load(string modulePath)
        {
            string path = Path.Combine(modulePath, FileName);
            if (!File.Exists(path))
                return new CollectionFile();
            try
            {
                var file = JsonSerializer.Deserialize<CollectionFile>(File.ReadAllText(path));
                if (file == null) return new CollectionFile();
                file.Cards = file.Cards ?? new List<CollectionCard>();
                file.Effects = file.Effects ?? new List<CardEffectGroup>();
                file.Packs = file.Packs ?? new List<CardPack>();
                return file;
            }
            catch
            {
                return new CollectionFile();
            }
        }

        public void Save(string modulePath)
        {
            string path = Path.Combine(modulePath, FileName);
            // don't create cards.json for modules that never had a collection
            if (IsEmpty && !File.Exists(path))
                return;
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(this, options));
        }

        /// <summary>Next sequential card number for a type (Soul Plates are always 0).</summary>
        public int NextNumber(string type)
        {
            if (type == CollectionCard.TypeSoulPlate) return 0;
            int max = 0;
            foreach (var c in Cards)
                if (c.Type == type && c.Number > max) max = c.Number;
            return max + 1;
        }

        /// <summary>Cards ordered for display: by type, then number, Souls alphabetical.</summary>
        public List<CollectionCard> Ordered()
        {
            int TypeOrder(string t)
            {
                int i = Array.IndexOf(CollectionCard.AllTypes, t);
                return i >= 0 ? i : 99;
            }
            return Cards
                .OrderBy(c => TypeOrder(c.Type))
                .ThenBy(c => c.Number)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
