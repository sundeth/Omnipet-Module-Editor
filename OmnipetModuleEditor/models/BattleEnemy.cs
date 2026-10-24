using System.Text.Json.Serialization;

public class BattleEnemy
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("power")]
    public int Power { get; set; }

    [JsonPropertyName("attribute")]
    public string Attribute { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("area")]
    public int Area { get; set; }

    [JsonPropertyName("round")]
    public int Round { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("handicap")]
    public int Handicap { get; set; }

    [JsonPropertyName("prize")]
    public string Prize { get; set; }

    /// <summary>A card pack (cards.json pack id) won for beating this enemy,
    /// as Prize is an item; opened into the collection. Null for none.</summary>
    [JsonPropertyName("prize_card_pack")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string PrizeCardPack { get; set; }

    /// <summary>Percent chance the prize (item or card pack) drops; null,
    /// and left out of battle.json, for always (100).</summary>
    [JsonPropertyName("prize_chance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PrizeChance { get; set; }

    [JsonPropertyName("unlock")]
    public string Unlock { get; set; }

    /// <summary>The attacks this enemy throws in the adventure, one per
    /// round, on the module's battle scale (Colour lines: 1 normal, 2 super).
    /// Null, and left out of battle.json, for the stage's own pattern.</summary>
    [JsonPropertyName("attack_pattern")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[] AttackPattern { get; set; }

    [JsonPropertyName("atk_main")]
    public int AtkMain { get; set; }

    [JsonPropertyName("atk_alt")]
    public int AtkAlt { get; set; }

    [JsonPropertyName("atk_alt_2")]
    public int AtkAlt2 { get; set; }

    [JsonPropertyName("stage")]
    public int Stage { get; set; }

    [JsonPropertyName("special_encounter")]
    public bool SpecialEncounter { get; set; }

    /// <summary>
    /// Battling this enemy registers it in the player's per-module Friend
    /// list (used by Xros temporary evolutions and the digidex Friends view).
    /// </summary>
    [JsonPropertyName("friend")]
    public bool Friend { get; set; }

    /// <summary>The currency (shop.json) this enemy charges and pays in;
    /// null for none. Left out of battle.json when unset, so a module with no
    /// currencies does not change.</summary>
    [JsonPropertyName("currency")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Currency { get; set; }

    /// <summary>What entering this enemy's area costs, in its currency. Only
    /// round 1 enemies carry it: it is the area's entry fee (the Digivice
    /// iC's Coliseums charge 0, 100, 500 and 1000 P).</summary>
    [JsonPropertyName("currency_cost")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CurrencyCost { get; set; }

    /// <summary>What beating this enemy pays, in its currency. A lost battle
    /// pays nothing.</summary>
    [JsonPropertyName("currency_reward")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CurrencyReward { get; set; }
}