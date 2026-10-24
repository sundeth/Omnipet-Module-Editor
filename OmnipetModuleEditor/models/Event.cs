using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    /// <summary>What an event does. EnemyBattle fights the special
    /// encounter at Area/Round; ItemPackage gives Item (or one of Item Pool
    /// at random, as BattleRun can); FriendBattle fights
    /// one of the Friends a party pet still needs; PoolBattle a special
    /// encounter drawn from Pools by the pet's Pool Field, each pool a
    /// rotation; BattleRun every special round of Area in turn, Item paid
    /// after Reward Round (0: the last); ShopAccess opens the event-only
    /// shop Shop; CardBattle is the Vital Bracelet's random battle, a
    /// monster of the pet's own version by its encounter_chances, each win
    /// paying the next of Item Sequence.</summary>
    public enum EventType
    {
        EnemyBattle,
        ItemPackage,
        FriendBattle,
        PoolBattle,
        BattleRun,
        ShopAccess,
        CardBattle
    }

    public class Event
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("global")]
        public bool Global { get; set; }

        [JsonPropertyName("type")]
        public EventType Type { get; set; }

        [JsonPropertyName("chance_percent")]
        public double ChancePercent { get; set; }

        [JsonPropertyName("area")]
        public int? Area { get; set; }

        [JsonPropertyName("round")]
        public int? Round { get; set; }

        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("item_quantity")]
        public int? ItemQuantity { get; set; }

        /// <summary>When the event is considered: random (the hourly roll,
        /// Chance a weight), adventure / area_clear / connection (after such
        /// a battle, Chance in 100) or schedule (at the pet's
        /// encounter_time_group times). Null means random.</summary>
        [JsonPropertyName("trigger")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Trigger { get; set; }

        /// <summary>For a battle trigger: win, loss or any.</summary>
        [JsonPropertyName("outcome")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Outcome { get; set; }

        /// <summary>For a connection trigger: the device the other side must
        /// be; -1 any device but the pet's own; null any.</summary>
        [JsonPropertyName("opponent_device_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? OpponentDeviceVersion { get; set; }

        /// <summary>For a connection trigger: the battle format it must be
        /// fought on ("IC", "DMM" ...); null any. Device numbers repeat across
        /// formats: the iC's DigiShop 10 opens against a 10X (IC, 1), its 13
        /// and 14 against a Digimon Mini (DMM), whose Ver.1 also reads 1.</summary>
        [JsonPropertyName("protocol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Protocol { get; set; }

        /// <summary>For a battle trigger: events sharing a draw are one roll,
        /// at most one of those fitting the battle happening, each with its
        /// Chance of the hundred in turn (the iC 20X after a connection: a
        /// shop by the device beaten, or the Coliseum run). Null: rolled on
        /// its own.</summary>
        [JsonPropertyName("draw")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Draw { get; set; }

        /// <summary>The pet versions the event is for; null for all.</summary>
        [JsonPropertyName("versions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int> Versions { get; set; }

        /// <summary>PoolBattle: pool key -> the special encounters, in turn
        /// ("" for a turn with none).</summary>
        [JsonPropertyName("pools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, List<string>> Pools { get; set; }

        /// <summary>PoolBattle: the monster field whose value picks the
        /// pool (encounter_opponent_group on the Godzilla devices).</summary>
        [JsonPropertyName("pool_field")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PoolField { get; set; }

        /// <summary>ShopAccess: the shop.json shop it opens.</summary>
        [JsonPropertyName("shop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Shop { get; set; }

        /// <summary>ShopAccess: minutes the shop stays open; 0 for good; -1
        /// one visit (the alert after a battle, gone once the shop is left).</summary>
        [JsonPropertyName("access_minutes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int AccessMinutes { get; set; }

        /// <summary>BattleRun: the round after which Item is paid; 0 the last.</summary>
        [JsonPropertyName("reward_round")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int RewardRound { get; set; }

        /// <summary>FriendBattle after a battle: [min, max] minutes it is
        /// held back. Null for none.</summary>
        [JsonPropertyName("delay_minutes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] DelayMinutes { get; set; }

        /// <summary>CardBattle: the item each win pays, in turn ("" a win
        /// without one); the pet's place is kept per event. Null for none.</summary>
        [JsonPropertyName("item_sequence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> ItemSequence { get; set; }

        /// <summary>ItemPackage or BattleRun: one of these items at random,
        /// drawn each time the event happens, instead of Item (the iC 20X's
        /// Special Coliseum: any Attack +2 weapon). Null for none.</summary>
        [JsonPropertyName("item_pool")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> ItemPool { get; set; }

        /// <summary>For an adventure or area_clear trigger: the adventure
        /// areas whose battles roll it (the area fought in, or the area just
        /// cleared). Null for any area. Digimon World's recruits are met in
        /// their own region.</summary>
        [JsonPropertyName("trigger_areas")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<int> TriggerAreas { get; set; }

        /// <summary>A special encounter event that ends once its Digimon is
        /// the module's Friend: Digimon World's recruits are met until
        /// recruited (a battle.json enemy with Friend set registers it).</summary>
        [JsonPropertyName("until_friend")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool UntilFriend { get; set; }

        // Grid helpers: the lists as "1, 2" text. Not in the file.

        [JsonIgnore]
        public string VersionsText
        {
            get { return Versions == null ? "" : string.Join(", ", Versions); }
            set { var v = ParseInts(value); Versions = v.Count > 0 ? v : null; }
        }

        [JsonIgnore]
        public string TriggerAreasText
        {
            get { return TriggerAreas == null ? "" : string.Join(", ", TriggerAreas); }
            set { var v = ParseInts(value); TriggerAreas = v.Count > 0 ? v : null; }
        }

        [JsonIgnore]
        public string DelayText
        {
            get { return DelayMinutes == null ? "" : string.Join("-", DelayMinutes); }
            set { var v = ParseInts(value); DelayMinutes = v.Count == 0 ? null : new[] { v[0], v.Count > 1 ? v[1] : v[0] }; }
        }

        [JsonIgnore]
        public string OpponentDeviceText
        {
            get { return OpponentDeviceVersion?.ToString() ?? ""; }
            set { var v = ParseInts(value); OpponentDeviceVersion = v.Count > 0 ? v[0] : (int?)null; }
        }

        [JsonIgnore]
        public string ProtocolText
        {
            get => Protocol ?? "";
            set => Protocol = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }

        [JsonIgnore]
        public string DrawText
        {
            get => Draw ?? "";
            set => Draw = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [JsonIgnore]
        public string PoolsText
        {
            get { return Pools == null ? "" : Pools.Count + " pool" + (Pools.Count == 1 ? "" : "s"); }
        }

        [JsonIgnore]
        public string SequenceText
        {
            get
            {
                if (ItemSequence == null) return "";
                int items = ItemSequence.FindAll(n => !string.IsNullOrEmpty(n)).Count;
                return items + " of " + ItemSequence.Count + " wins";
            }
        }

        [JsonIgnore]
        public string ItemPoolText
        {
            get { return ItemPool == null ? "" : ItemPool.Count + " item" + (ItemPool.Count == 1 ? "" : "s"); }
        }

        /// <summary>Whole numbers in <paramref name="text"/>, separated by
        /// commas, spaces, semicolons or dashes (a leading minus is kept).</summary>
        public static List<int> ParseInts(string text)
        {
            var values = new List<int>();
            foreach (var word in (text ?? "").Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = word.StartsWith("-") ? new[] { word } : word.Split('-');
                foreach (var part in parts)
                {
                    int n;
                    if (int.TryParse(part, out n)) values.Add(n);
                }
            }
            return values;
        }
    }

    /// <summary>events.json's Connect Points block: a pet's points from
    /// battles (with other devices and pets, and adventure battles where
    /// Battles names them), and what filling them gives. A cap of 0
    /// switches them off; a cap of 1 with empty-item rewards is a drop table
    /// rolled after every battle that counts (the Vital Bracelets).</summary>
    public class ConnectPoints
    {
        [JsonPropertyName("cap")]
        public int Cap { get; set; }

        [JsonPropertyName("win")]
        public int Win { get; set; } = 1;

        [JsonPropertyName("loss")]
        public int Loss { get; set; } = 1;

        /// <summary>The battles that give points: "connection" (another
        /// device or pet) and "adventure" (an adventure battle). Null (left
        /// out of the file): connections only, the Digivice iC's meter.</summary>
        [JsonPropertyName("battles")]
        public List<string> Battles { get; set; }

        [JsonPropertyName("rewards")]
        public List<ConnectReward> Rewards { get; set; } = new List<ConnectReward>();
    }

    public class ConnectReward
    {
        /// <summary>The pet version that draws it; null for every version.</summary>
        [JsonPropertyName("version")]
        public int? Version { get; set; }

        /// <summary>One of the rewards the pet's version can draw, each listed
        /// one equally likely: listing an item twice doubles its chance. ""
        /// is a draw that gives nothing.</summary>
        [JsonPropertyName("item")]
        public string Item { get; set; } = "";
    }
}
