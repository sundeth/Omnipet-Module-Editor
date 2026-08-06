using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OmnipetModuleEditor.Models
{
    /// <summary>
    /// A physical device presentation available in a module. Device versions are
    /// deliberately independent from monster/evolution versions: they describe
    /// the value understood by the original hardware and battle protocol.
    /// </summary>
    public class DeviceDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "New Device";

        /// <summary>PNG file name without its extension, stored in devices/.</summary>
        [JsonPropertyName("sprite")]
        public string Sprite { get; set; } = "";

        [JsonPropertyName("device_version")]
        public int DeviceVersion { get; set; } = 1;

        /// <summary>Internal name of a module background.</summary>
        [JsonPropertyName("background")]
        public string Background { get; set; } = "";

        /// <summary>Background top-left position in source device-sprite pixels.</summary>
        [JsonPropertyName("background_x")]
        public int BackgroundX { get; set; } = 0;

        [JsonPropertyName("background_y")]
        public int BackgroundY { get; set; } = 0;

        /// <summary>Background scale as a percentage, from 1 to 100.</summary>
        [JsonPropertyName("background_scale")]
        public int BackgroundScale { get; set; } = 100;

        [JsonPropertyName("eggs")]
        public List<DeviceEgg> Eggs { get; set; } = new List<DeviceEgg>();
    }

    /// <summary>Egg associated with a device. Name + module version identify it.</summary>
    public class DeviceEgg
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
    }
}
