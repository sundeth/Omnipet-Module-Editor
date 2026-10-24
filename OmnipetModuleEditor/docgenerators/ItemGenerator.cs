using OmnipetModuleEditor.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OmnipetModuleEditor.docgenerators
{
    internal class ItemGenerator
    {
        public static void GenerateItemsPage(string docPath, List<Item> items)
        {
            GenerateItemsPage(docPath, items, null);
        }

        /// <summary>The items, and with <paramref name="modulePath"/> the
        /// module's shops (shop.json): currency, how each opens, its stock.</summary>
        public static void GenerateItemsPage(string docPath, List<Item> items, string modulePath)
        {
            string template = GeneratorUtils.GetTemplateContent("items.html");
            var sb = new StringBuilder();

            if (items != null && items.Count > 0)
            {
                foreach (var item in items)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine("  <td>");
                    sb.AppendLine("    <div class=\"item-name-container\">");
                    if (!string.IsNullOrEmpty(item.SpriteName))
                    {
                        sb.AppendLine($"      <img src=\"../items/{item.SpriteName}.png\" alt=\"{item.Name}\" class=\"item-icon\" onerror=\"this.style.display='none'\">");
                    }
                    sb.AppendLine($"      <span class=\"item-name\">{item.Name}</span>");
                    sb.AppendLine("    </div>");
                    sb.AppendLine("  </td>");
                    sb.AppendLine($"  <td>{item.Description ?? ""}</td>");
                    sb.AppendLine($"  <td>{item.Effect ?? ""}{(string.IsNullOrEmpty(item.Currency) ? "" : " (" + item.Currency + ")")}</td>");
                    sb.AppendLine($"  <td>{item.Status ?? ""}</td>");
                    sb.AppendLine($"  <td>{item.Amount}</td>");
                    sb.AppendLine($"  <td>{item.WeightGain}</td>");
                    sb.AppendLine($"  <td>{(item.ProtocolValue != 0 ? item.ProtocolValue.ToString() : "")}</td>");
                    sb.AppendLine($"  <td>{(item.AttackSprite != 0 ? item.AttackSprite.ToString() : "")}</td>");
                    sb.AppendLine($"  <td>{item.Digisoul ?? ""}</td>");
                    sb.AppendLine($"  <td>{ExtraEffectsText(item)}</td>");
                    sb.AppendLine($"  <td>{DetailsText(item)}</td>");
                    sb.AppendLine("</tr>");
                }
            }

            string content = template.Replace("#ITEMSDATA", sb.ToString());
            string shops = ShopsHtml(modulePath);
            if (shops.Length > 0)
                content = content.Contains("</body>") ? content.Replace("</body>", shops + "</body>") : content + shops;
            File.WriteAllText(Path.Combine(docPath, "items.html"), content);
        }

        /// <summary>The use rules: how long a boost lasts, whether it ends at
        /// midnight or is kept after use, which stages and target take it.</summary>
        private static string DetailsText(Item item)
        {
            var parts = new List<string>();
            if (item.BoostTime > 0) parts.Add($"lasts {item.BoostTime} min");
            if (item.ExpiresAtMidnight) parts.Add("ends at midnight");
            if (item.Reusable) parts.Add("reusable");
            if (item.MinStage > 0 || item.MaxStage > 0)
                parts.Add($"stages {item.MinStage}-{(item.MaxStage > 0 ? item.MaxStage.ToString() : "+")}");
            if (!string.IsNullOrEmpty(item.Target)) parts.Add($"target {item.Target}");
            if (!string.IsNullOrEmpty(item.ComponentItem)) parts.Add($"made from {item.ComponentItem}");
            return string.Join("<br>", parts);
        }

        /// <summary>A section per shop of shop.json, or "" without one.</summary>
        private static string ShopsHtml(string modulePath)
        {
            if (string.IsNullOrEmpty(modulePath)) return "";
            string path = Path.Combine(modulePath, "shop.json");
            if (!File.Exists(path)) return "";
            ShopFile file;
            try { file = System.Text.Json.JsonSerializer.Deserialize<ShopFile>(File.ReadAllText(path)); }
            catch { return ""; }
            if (file?.Shops == null || file.Shops.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"content-section\"><h2>Shops</h2>");
            foreach (var shop in file.Shops)
            {
                var currency = file.Currencies?.Find(c => c.Name == shop.Currency);
                string unit = string.IsNullOrEmpty(currency?.Unit) ? (shop.Currency ?? "") : currency.Unit;
                string opens = shop.EventOnly ? "opened by an event" : (string.IsNullOrEmpty(shop.Unlock) ? "always open" : $"opens with {shop.Unlock}");
                sb.AppendLine($"<h3>{shop.Name}</h3><p>{shop.Currency}, {opens}.</p>");
                sb.AppendLine("<table class=\"items-table\"><thead><tr><th>Item</th><th>Price</th><th>Versions</th><th>Unlock</th></tr></thead><tbody>");
                foreach (var stock in shop.Items ?? new List<ShopItem>())
                {
                    string name = !string.IsNullOrEmpty(stock.CardOffer) ? $"One card of {stock.CardOffer}, a different one each day"
                        : string.IsNullOrEmpty(stock.CardPack) ? stock.Item : $"Card pack {stock.CardPack}";
                    string price = !string.IsNullOrEmpty(stock.CardOffer) && stock.RarityPrices != null && stock.RarityPrices.Count > 0
                        ? string.Join(", ", CollectionCard.Rarities.Select(r =>
                            $"{r} {(stock.RarityPrices.TryGetValue(r, out int p) ? p : stock.Price)} {unit}"))
                        : $"{stock.Price} {unit}";
                    string versions = stock.Versions == null || stock.Versions.Count == 0 ? "all" : string.Join(", ", stock.Versions);
                    sb.AppendLine($"<tr><td>{name}</td><td>{price}</td><td>{versions}</td><td>{stock.Unlock}</td></tr>");
                }
                sb.AppendLine("</tbody></table>");
            }
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        /// <summary>One line per extra effect: "status_change reliability +1",
        /// "currency +1000 Fight Money".</summary>
        private static string ExtraEffectsText(Item item)
        {
            if (item.ExtraEffects == null || item.ExtraEffects.Count == 0)
                return "";
            return string.Join("<br>", item.ExtraEffects.Select(e => string.Join(" ",
                new[] { e.Effect, e.Status, (e.Amount >= 0 ? "+" : "") + e.Amount, e.Currency, e.Digisoul }
                    .Where(part => !string.IsNullOrEmpty(part)))));
        }
    }
}
