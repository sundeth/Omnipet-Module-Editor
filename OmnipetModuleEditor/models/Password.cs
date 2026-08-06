using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    /// <summary>
    /// A redeemable password (codes.json). Depending on Type, one of the
    /// item/pet/unlock/encounter field groups applies.
    /// </summary>
    public class Password
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        /// <summary>item | pet | unlock | encounter</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        // type == item
        [JsonPropertyName("item")]
        public string Item { get; set; }

        [JsonPropertyName("amount")]
        public int? Amount { get; set; }

        // type == pet
        [JsonPropertyName("pet")]
        public string Pet { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }

        // type == unlock
        [JsonPropertyName("unlock")]
        public string Unlock { get; set; }

        // type == encounter (round is always 1)
        [JsonPropertyName("area")]
        public int? Area { get; set; }

        /// <summary>Cooldown in minutes; 0 = no cooldown, -1 = one use only.</summary>
        [JsonPropertyName("cooldown")]
        public int Cooldown { get; set; }
    }

    public enum PasswordTypeEnum
    {
        item,
        pet,
        unlock,
        encounter
    }
}
