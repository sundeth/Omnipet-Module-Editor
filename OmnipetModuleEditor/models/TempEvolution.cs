using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    /// <summary>
    /// A temporary (battle-only) evolution: the pet transforms when the
    /// conditions are met during battle and reverts afterwards.  Stored on
    /// the pet as the "temporary-evolution" list, separate from the standard
    /// "evolve" list.
    /// </summary>
    public class TempEvolution
    {
        public const string TYPE_MODE_CHANGE = "Mode Change";
        public const string TYPE_XROS = "Xros";

        [JsonPropertyName("to")]
        public string To { get; set; }

        /// <summary>"Mode Change" or "Xros".</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = TYPE_MODE_CHANGE;

        /// <summary>[min, max] strength range; null = disabled; 999999 = no upper limit.</summary>
        [JsonPropertyName("strength")]
        public int[] Strength { get; set; }

        [JsonPropertyName("megahit")]
        public bool? Megahit { get; set; }

        /// <summary>Name of a module unlock that must be obtained.</summary>
        [JsonPropertyName("unlock")]
        public string Unlock { get; set; }

        /// <summary>Friend pets that must be registered in the player's digidex.</summary>
        [JsonPropertyName("friend")]
        public List<string> Friend { get; set; }

        /// <summary>[min, max] Friends on the module's Friend list; null =
        /// disabled; 999999 = no upper limit (DMXW's Shoutmon X7: Superior
        /// Mode, "13 or more Digimon in the Friend list").</summary>
        [JsonPropertyName("friend_count")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] FriendCount { get; set; }

        /// <summary>[min, max] area the battle is fought in: an adventure area
        /// of the pet's own module, 0 for any other battle (events, Versus,
        /// DCom); null = anywhere; 999999 = no upper limit. DMXW's
        /// Arresterdramon: Superior Mode 0-19 ("not available during World
        /// 20"), its Brave Snatcher 20-20 ("only available during World 20").</summary>
        [JsonPropertyName("battle_area")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] BattleArea { get; set; }

        /// <summary>DP the form costs: offered only to a pet holding that
        /// much, spent when taken; null or 0 = free (DMXW's DigiXros, humulos
        /// "Required DP", 1 for Shoutmon X2 to 7 for Shoutmon X7).</summary>
        [JsonPropertyName("xros_cost")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? XrosCost { get; set; }

        /// <summary>Xros only: background file name (no extension) from the
        /// module's backgrounds folder, shown during the xros animation.</summary>
        [JsonPropertyName("background")]
        public string Background { get; set; }

        /// <summary>Xros only: animation name (files "name_1".."name_5" in the
        /// module's animations folder) played during the xros animation.</summary>
        [JsonPropertyName("animation")]
        public string Animation { get; set; }

        public TempEvolution Clone()
        {
            return new TempEvolution
            {
                To = To,
                Type = Type,
                Strength = Strength != null ? (int[])Strength.Clone() : null,
                Megahit = Megahit,
                Unlock = Unlock,
                Friend = Friend != null ? new List<string>(Friend) : null,
                FriendCount = FriendCount != null ? (int[])FriendCount.Clone() : null,
                BattleArea = BattleArea != null ? (int[])BattleArea.Clone() : null,
                XrosCost = XrosCost,
                Background = Background,
                Animation = Animation,
            };
        }
    }
}
