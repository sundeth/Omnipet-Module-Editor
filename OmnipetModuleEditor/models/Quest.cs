using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OmnipetModuleEditor.Models
{
    // Daily_Gift (7): nothing to do; offered every day, ready to claim (DW1's Meat Farm).
    public enum QuestType { Boss, Training, Battle, Feeding, Evolution, Armor_Evolution, Jogress, Daily_Gift }
    public enum RewardType { Item, Trophy, Experience, Vital_Values }

    public class Quest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public QuestType QuestType { get; set; }

        [JsonPropertyName("target_amount_range")]
        public int[] TargetAmountRange { get; set; }

        [JsonPropertyName("reward_type")]
        public RewardType RewardType { get; set; }

        [JsonPropertyName("reward_value")]
        public string RewardValue { get; set; }

        [JsonPropertyName("reward_quantity")]
        public int RewardQuantity { get; set; }
    }
}
