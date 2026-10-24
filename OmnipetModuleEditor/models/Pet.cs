using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    public class Pet
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("stage")]
        public int Stage { get; set; }

        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("special")]
        public bool Special { get; set; }

        [JsonPropertyName("special_key")]
        public string SpecialKey { get; set; }

        [JsonPropertyName("sleeps")]
        public string Sleeps { get; set; }

        [JsonPropertyName("wakes")]
        public string Wakes { get; set; }

        [JsonPropertyName("atk_main")]
        public int AtkMain { get; set; }

        [JsonPropertyName("atk_alt")]
        public int AtkAlt { get; set; }

        [JsonPropertyName("atk_alt_2")]
        public int AtkAlt2 { get; set; }

        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("poop_timer")]
        public int PoopTimer { get; set; }

        [JsonPropertyName("energy")]
        public int Energy { get; set; }

        [JsonPropertyName("min_weight")]
        public int MinWeight { get; set; }

        [JsonPropertyName("evol_weight")]
        public int EvolWeight { get; set; }

        [JsonPropertyName("stomach")]
        public int Stomach { get; set; }

        [JsonPropertyName("hunger_loss")]
        public int HungerLoss { get; set; }

        [JsonPropertyName("strength_loss")]
        public int StrengthLoss { get; set; }

        [JsonPropertyName("heal_doses")]
        public int HealDoses { get; set; }

        [JsonPropertyName("power")]
        public int Power { get; set; }

        /// <summary>
        /// The shake rate this Digimon's charge asks for, in the Digivice
        /// iC's own RPM. Per Digimon rather than per stage: the iC roster
        /// gives every form a figure between 50 and 85, and hitting it is
        /// what throws the Hyper x5 attack row.
        ///
        /// 0 means the module names none, and the RPM minigame falls back to
        /// the playbook's stage/attribute table - which is what a pet from
        /// any other module on that wire reads. Only the RPM charge consumes
        /// it; leave it 0 everywhere else.
        /// </summary>
        [JsonPropertyName("rpm")]
        public int Rpm { get; set; }

        [JsonPropertyName("attribute")]
        public string Attribute { get; set; }

        /// <summary>
        /// Native DigiSoul family used by Digivice iC devices. An empty value
        /// means the device/profile does not assign a DigiSoul family.
        /// </summary>
        [JsonPropertyName("digisoul")]
        public string Digisoul { get; set; } = "";

        /// <summary>Base probability, from 0 through 100 percent, that this
        /// Digimon is injured after a battle loss.</summary>
        [JsonPropertyName("injury_probability")]
        public int InjuryProbability { get; set; }

        /// <summary>The Godzilla devices' scheduled encounters: the group of
        /// the events.json schedule whose times this Digimon's encounters
        /// come at. Null, and left out of the file, for none.</summary>
        [JsonPropertyName("encounter_time_group")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EncounterTimeGroup { get; set; }

        /// <summary>Which pool of a scheduled encounter event this Digimon
        /// meets its opponents from (the event's Pool Field names this
        /// field). Null, and left out of the file, for none.</summary>
        [JsonPropertyName("encounter_opponent_group")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EncounterOpponentGroup { get; set; }

        /// <summary>The Vital Bracelet's random battle (a CardBattle event):
        /// this character's chance in percent of being met by a pet of its
        /// version at stage 1-4, 5-7 and 8 (the card's three battle pools).
        /// A null entry is none; null, and left out of the file, for a
        /// Digimon not on a card.</summary>
        [JsonPropertyName("encounter_chances")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int?[] EncounterChances { get; set; }

        [JsonPropertyName("condition_hearts")]
        public int ConditionHearts { get; set; }

        [JsonPropertyName("jogress_available")]
        public bool JogressAvailable { get; set; }

        // Legacy misspelled key. Set-only, so an older module still loads
        // and saving it writes only the corrected name.
        [JsonPropertyName("jogress_avaliable")]
        public bool JogressAvaliable { set { JogressAvailable = value; } }

        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        /// <summary>Which round the special move fires on, and so which
        /// round earns +2 AP: Stoic 1, Active 2, Normal 3, Indoor 4,
        /// Lazy 5. Devices with no personality data use Normal.</summary>
        [JsonPropertyName("personality")]
        public string Personality { get; set; } = "Normal";

        /// <summary>Labels an evolution can count defeats of (its
        /// <c>defeat_tag</c>), such as the PENGZ's "G-Monster". Null: none.</summary>
        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("star")]
        public int Star { get; set; }

        [JsonPropertyName("attack")]
        public int Attack { get; set; }

        [JsonPropertyName("critical_turn")]
        public int CriticalTurn { get; set; }

        [JsonPropertyName("evolve")]
        public List<Evolution> Evolve { get; set; }

        /// <summary>Battle-only temporary evolutions (Mode Change / Xros).</summary>
        [JsonPropertyName("temporary-evolution")]
        public List<TempEvolution> TempEvolve { get; set; }

        /// <summary>
        /// "Normal" (null — not saved), "Unobtainable" (never appears in the
        /// digidex) or "Friend" (digidex "Friends" list; used by Xros).
        /// </summary>
        [JsonPropertyName("availability")]
        public string Availability { get; set; }

        // Legacy misspelled key. Set-only, so an older module still loads
        // and saving it writes only the corrected name.
        [JsonPropertyName("avaliability")]
        public string Avaliability { set { Availability = value; } }
    }

    /// <summary>The Evolution Stage names shown for stage numbers 0-8 (the
    /// project's terms; StageEnum keeps the old dub names for code).</summary>
    public static class Stages
    {
        public static readonly string[] Names =
        {
            "Egg", "Baby I", "Baby II", "Child", "Adult",
            "Perfect", "Ultimate", "Super Ultimate", "Super Ultimate+"
        };

        public static string Name(int stage) =>
            stage >= 0 && stage < Names.Length ? Names[stage] : "Stage " + stage;
    }

    public enum StageEnum
    {
        EGG = 0,
        FRESH = 1,
        IN_TRAINING = 2,
        ROOKIE = 3,
        CHAMPION = 4,
        ULTIMATE = 5,
        MEGA = 6,
        SUPER_ULTIMATE = 7,
        SUPER_ULTIMATE_PLUS = 8
    }

    public enum AttributeEnum
    {
        FREE = 0,
        DATA = 1,
        VIRUS = 2,
        VACCINE = 3
    }
}
