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
                Background = Background,
                Animation = Animation,
            };
        }
    }
}
