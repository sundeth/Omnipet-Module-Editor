using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    public class Item
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("sprite_name")]
        public string SpriteName { get; set; }

        [JsonPropertyName("effect")]
        public string Effect { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("boost_time")]
        public int BoostTime { get; set; }

        [JsonPropertyName("component_item")]
        public string ComponentItem { get; set; }

        /// <summary>The item's own signed weight change, made to every pet
        /// that takes it whatever the effect, floored at the pet's minimum
        /// weight: -99 takes a pet to its minimum.</summary>
        [JsonPropertyName("weight_gain")]
        public int WeightGain { get; set; }

        /// <summary>The item is discarded at midnight rather than kept. The
        /// Digimon Twin's special foods all revert to Protein at 00:00, so
        /// they cannot be hoarded.</summary>
        [JsonPropertyName("expires_at_midnight")]
        public bool ExpiresAtMidnight { get; set; } = false;

        /// <summary>What the item puts on its module's battle wire while its
        /// status_boost is active; 0 sends nothing. On the Digivice iC wire it
        /// is the equipped weapon's code in packet 3 (a Beast Claw sent 2, a
        /// Water Drop 3, a Fowl Tornado 4).</summary>
        [JsonPropertyName("protocol_value")]
        public int ProtocolValue { get; set; } = 0;

        /// <summary>The ordinary attack sprite the pet throws while this
        /// item's status_boost is active: an id in the module's atk folder;
        /// 0 changes nothing. A Digivice iC weapon draws its family's sprite
        /// (Dragon 3, Beast 8, Bird 11; the any-family weapons 4).</summary>
        [JsonPropertyName("attack_sprite")]
        public int AttackSprite { get; set; } = 0;

        /// <summary>A DigiSoul family, or empty for none. With effect dna it
        /// is the DNA the item adds to a pet (Amount of it). With
        /// status_boost it restricts the item to pets of that family: a
        /// Digivice iC family weapon.</summary>
        [JsonPropertyName("digisoul")]
        public string Digisoul { get; set; } = "";

        /// <summary>The shop.json currency a currency effect pays out
        /// (Amount of it). Null, and left out of the file, when unused.</summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        /// <summary>Further effects, applied to the pets that took the item
        /// after its main one, which still decides who can take it: the
        /// Digivice iC's sweets also give Reliability +1, its Pineapple Fight
        /// Money. Null, and left out of the file, when there are none.</summary>
        [JsonPropertyName("extra_effects")]
        public List<ItemEffect> ExtraEffects { get; set; }

        /// <summary>Using the item does not use it up: the Digivice iC's Meat
        /// and medicines, its C'mon Switch, and the accessories, which are
        /// put on and taken off again.</summary>
        [JsonPropertyName("reusable")]
        public bool Reusable { get; set; } = false;

        /// <summary>What an evolve item turns a pet into (a monster name on
        /// the pet's version), or the battle.json special encounter an
        /// encounter item starts (the enemy's name). Null, and left out of
        /// the file, when unused.</summary>
        [JsonPropertyName("target")]
        public string Target { get; set; }

        /// <summary>The lowest stage that can take the item, 0 for any: the
        /// iC's hidden-Ultimate items work from Child (3), its Aura on
        /// Ultimates (6).</summary>
        [JsonPropertyName("min_stage")]
        public int MinStage { get; set; } = 0;

        /// <summary>The highest stage that can take the item, 0 for no
        /// limit: Digimon World's evolution items work only one stage below
        /// their Digimon (Min Stage and Max Stage the same).</summary>
        [JsonPropertyName("max_stage")]
        public int MaxStage { get; set; } = 0;

        /// <summary>The families an item can name: the cards' DNA values
        /// without the special "X of each".</summary>
        public static readonly string[] DigisoulValues =
            { "", "Beast", "Bird", "Machine", "Water", "Dragon", "Insect", "Holy", "Dark" };
    }

    /// <summary>One of an item's extra effects: the main effect's fields.
    /// A status_change row is a plain change of the stat (never refused, no
    /// weight), stopping at the stat's limits, so +99 fills it and -99
    /// empties it. A currency row pays once per use.</summary>
    public class ItemEffect
    {
        [JsonPropertyName("effect")]
        public string Effect { get; set; } = "";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("boost_time")]
        public int BoostTime { get; set; }

        /// <summary>For a currency row: which currency. Null when unused.</summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        /// <summary>For a dna row: which family; null uses the item's.</summary>
        [JsonPropertyName("digisoul")]
        public string Digisoul { get; set; }

        /// <summary>For an item row: the item given, Amount of it (a bundle
        /// item's contents). Null when unused.</summary>
        [JsonPropertyName("item")]
        public string Item { get; set; }
    }

    /// <summary>What using an item does. currency pays Amount of the item's
    /// Currency; devolve returns the pet to the form it evolved from. The
    /// Digivice iC's: cure heals the Status ailment (sick, injury, or empty
    /// for either); bedding sleeps the pet Amount minutes, heals it and
    /// forgives one care mistake; accessory is worn (Status head, face or
    /// aura) until used again; closeup brings the Digimon up to the screen;
    /// evolve turns the pet into Target; random_evolve into one of the
    /// next-stage forms its routes lead to; encounter starts the special
    /// encounter named Target; bundle hands over the items of its extra
    /// rows of effect item (the iC 20X's Weapon MAX). The Vital Bracelet
    /// BE's: multiplier multiplies the pet's gains of Status (comma-separated
    /// pp, bp, ap, hp, vp) by Amount for Boost Time minutes, one at a time;
    /// training_limit adds Amount minutes to a Limit timer Omnipet does not
    /// have yet (catalogued, refused in the game).</summary>
    public enum EffectEnum
    {
        status_change,
        status_boost,
        component,
        digimental,
        dna,
        currency,
        devolve,
        bedding,
        cure,
        accessory,
        closeup,
        evolve,
        random_evolve,
        encounter,
        bundle,
        multiplier,
        training_limit
    }

    public enum StatusEnum
    {
        hunger,
        strength,
        hp,
        attack,
        xai_roll,
        skip_to_boss,
        exp_multiplier,
        armor,
        power,
        vital_values,
        timer,
        dp,
        reliability,
        injury_rate,
        // A cure's ailment.
        sick,
        injury,
        // An accessory's place.
        head,
        face,
        aura,
        // A multiplier's targets (the Vital Bracelet BE) beside hp: PP
        // (trophies), BP, AP and the Vital Values a battle wins.
        pp,
        bp,
        ap,
        vp
    }

}