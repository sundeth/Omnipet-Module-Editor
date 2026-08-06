using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace OmnipetModuleEditor.docgenerators
{
    internal class DeviceGenerator
    {
        /// <summary>
        /// Generates the device page from module files. Kept independent from
        /// the legacy documentation driver while that driver remains UTF-16.
        /// </summary>
        public static void GenerateDevicesPage(string docPath, string modulePath)
        {
            GenerateDevicesPage(docPath, ReadDevices(modulePath), ReadModule(modulePath), modulePath);
        }

        public static void GenerateDevicesPage(string docPath, List<DeviceDefinition> devices, Module module)
        {
            GenerateDevicesPage(docPath, devices, module, null);
        }

        private static void GenerateDevicesPage(string docPath, List<DeviceDefinition> devices, Module module, string modulePath)
        {
            string template = GeneratorUtils.GetTemplateContent("devices.html");
            var output = new StringBuilder();
            var eggSpriteFiles = CopyEggSprites(docPath, devices, module, modulePath);

            foreach (var device in devices ?? new List<DeviceDefinition>())
            {
                string name = Html(device.Name);
                string sprite = Html(device.Sprite);
                string background = Html(device.Background);
                string backgroundFile = GetBackgroundFile(device.Background, module);
                output.AppendLine("<article class=\"device-card\">");
                output.AppendLine("  <div class=\"device-preview\">");
                if (!string.IsNullOrWhiteSpace(backgroundFile))
                {
                    output.AppendLine("    <img class=\"device-background\" src=\"../backgrounds/" + Html(backgroundFile) + "\" style=\"left:" + device.BackgroundX + "px; top:" + device.BackgroundY + "px; transform:scale(" + (Math.Max(1, device.BackgroundScale) / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ");\" alt=\"\">");
                }
                if (!string.IsNullOrWhiteSpace(sprite))
                    output.AppendLine("    <img class=\"device-sprite\" src=\"../devices/" + sprite + ".png\" alt=\"" + name + "\">");
                else
                    output.AppendLine("    <div class=\"missing-device-sprite\">No device sprite</div>");
                output.AppendLine("  </div>");
                output.AppendLine("  <div class=\"device-details\">");
                output.AppendLine("    <h2>" + name + "</h2>");
                output.AppendLine("    <p><strong>Device version:</strong> " + device.DeviceVersion + "</p>");
                output.AppendLine("    <p><strong>Background:</strong> " + (string.IsNullOrWhiteSpace(background) ? "None" : background) + "</p>");
                output.AppendLine("    <p><strong>Screen position:</strong> X " + device.BackgroundX + ", Y " + device.BackgroundY + ", " + Math.Max(1, device.BackgroundScale) + "%</p>");
                output.AppendLine("    <div class=\"device-eggs\">");
                foreach (var egg in device.Eggs ?? new List<DeviceEgg>())
                {
                    string eggName = Html(egg.Name);
                    output.AppendLine("      <div class=\"device-egg\" title=\"" + eggName + " (Version " + egg.Version + ")\">");
                    if (eggSpriteFiles.TryGetValue(EggKey(egg), out string eggSprite))
                    {
                        output.AppendLine("        <img src=\"assets/" + Html(eggSprite) + "\" alt=\"" + eggName + "\" onerror=\"this.onerror=null;this.src='assets/missing.png';\">");
                    }
                    else
                    {
                        output.AppendLine("        <div class=\"device-egg-missing\">Missing</div>");
                    }
                    output.AppendLine("        <span>V" + egg.Version + "</span>");
                    output.AppendLine("      </div>");
                }
                output.AppendLine("    </div>");
                output.AppendLine("  </div>");
                output.AppendLine("</article>");
            }

            File.WriteAllText(Path.Combine(docPath, "devices.html"), template.Replace("#DEVICESDATA", output.ToString()));
        }

        /// <summary>
        /// Documentation pages must not link into module sprite ZIPs: browsers
        /// cannot read those paths. Copy the first frame into documentation
        /// assets, using the same safe name as the chart generator.
        /// </summary>
        private static Dictionary<string, string> CopyEggSprites(
            string docPath, List<DeviceDefinition> devices, Module module, string modulePath)
        {
            var copied = new Dictionary<string, string>();
            string assetsPath = Path.Combine(docPath, "assets");
            Directory.CreateDirectory(assetsPath);

            foreach (var egg in (devices ?? new List<DeviceDefinition>())
                .Where(device => device != null)
                .SelectMany(device => device.Eggs ?? new List<DeviceEgg>())
                .Where(egg => egg != null && !string.IsNullOrWhiteSpace(egg.Name)))
            {
                string key = EggKey(egg);
                if (copied.ContainsKey(key)) continue;

                string fileName = GetSafeAssetFileName(egg.Name) + ".png";
                string destination = Path.Combine(assetsPath, fileName);
                if (!string.IsNullOrWhiteSpace(modulePath))
                {
                    try
                    {
                        using (var sprite = SpriteUtils.LoadSingleSprite(
                            egg.Name,
                            modulePath,
                            module?.NameFormat ?? SpriteUtils.DefaultNameFormat,
                            module?.PrimarySpriteFormat ?? "Color",
                            module?.SecondarySpriteFormat ?? "HD"))
                        {
                            sprite?.Save(destination, ImageFormat.Png);
                        }
                    }
                    catch
                    {
                        // A missing sprite is represented explicitly in the page.
                    }
                }

                if (File.Exists(destination)) copied[key] = fileName;
            }
            return copied;
        }

        private static string EggKey(DeviceEgg egg) => (egg.Name ?? "") + "\u001f" + egg.Version;

        private static string GetSafeAssetFileName(string name)
        {
            return (name ?? "").Replace(" ", "_")
                .Replace("(", "").Replace(")", "").Replace(":", "_")
                .Replace("/", "_").Replace("\\", "_").Replace("?", "_")
                .Replace("*", "_").Replace("\"", "_").Replace("<", "_")
                .Replace(">", "_").Replace("|", "_");
        }

        private static List<DeviceDefinition> ReadDevices(string modulePath)
        {
            string path = Path.Combine(modulePath, "devices.json");
            if (!File.Exists(path)) return new List<DeviceDefinition>();
            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    JsonElement devices;
                    if (document.RootElement.TryGetProperty("devices", out devices))
                        return JsonSerializer.Deserialize<List<DeviceDefinition>>(devices.GetRawText())
                            ?? new List<DeviceDefinition>();
                }
            }
            catch { }
            return new List<DeviceDefinition>();
        }

        private static Module ReadModule(string modulePath)
        {
            string path = Path.Combine(modulePath, "module.json");
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<Module>(File.ReadAllText(path)); }
            catch { return null; }
        }

        private static string GetBackgroundFile(string backgroundName, Module module)
        {
            if (string.IsNullOrWhiteSpace(backgroundName)) return "";
            var background = module?.Backgrounds?.FirstOrDefault(b => string.Equals(b.Name, backgroundName, StringComparison.OrdinalIgnoreCase));
            return background?.DayNight == true
                ? "bg_" + backgroundName + "_day.png"
                : "bg_" + backgroundName + ".png";
        }

        private static string Html(string value) => WebUtility.HtmlEncode(value ?? "");
    }
}
