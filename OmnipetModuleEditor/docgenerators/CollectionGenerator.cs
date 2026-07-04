using OmnipetModuleEditor.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace OmnipetModuleEditor.docgenerators
{
    /// <summary>
    /// Generates the Collection documentation page: card gallery, effects
    /// table (binary → effect descriptions) and card packs.
    /// </summary>
    internal class CollectionGenerator
    {
        public static void GenerateCollectionPage(string docPath, string modulePath)
        {
            var collection = CollectionFile.Load(modulePath);
            string template = GeneratorUtils.GetTemplateContent("collection.html");
            if (string.IsNullOrEmpty(template))
                return;

            string content = template
                .Replace("#CARDSDATA", BuildCards(collection))
                .Replace("#EFFECTSDATA", BuildEffects(collection))
                .Replace("#PACKSDATA", BuildPacks(collection));
            File.WriteAllText(Path.Combine(docPath, "collection.html"), content);
        }

        private static string Enc(string s) => WebUtility.HtmlEncode(s ?? "");

        private static string BuildCards(CollectionFile collection)
        {
            var sb = new StringBuilder();
            foreach (var card in collection.Ordered())
            {
                sb.AppendLine("<div class=\"collection-card\">");
                if (!string.IsNullOrEmpty(card.Sprites?.Front))
                    sb.AppendLine($"  <img src=\"../{Enc(card.Sprites.Front)}\" alt=\"{Enc(card.Name)}\" onerror=\"this.style.display='none'\">");
                string title = card.Type == CollectionCard.TypeSoulPlate
                    ? Enc(card.Name) : $"{Enc(card.Name)} #{card.Number}";
                sb.AppendLine($"  <div class=\"card-title\">{title}</div>");
                string series = string.IsNullOrEmpty(card.Series) ? "" : $" · Series {Enc(card.Series)}";
                sb.AppendLine($"  <div class=\"card-sub\">{Enc(card.Type)}{series}</div>");
                sb.AppendLine($"  <div class=\"card-sub rarity-{Enc(card.Rarity)}\">{Enc(card.Rarity)}</div>");
                sb.AppendLine($"  <div class=\"card-value\">{Enc(card.RfidValue)}</div>");
                sb.AppendLine("</div>");
            }
            return sb.ToString();
        }

        private static string BuildEffects(CollectionFile collection)
        {
            var sb = new StringBuilder();
            foreach (var group in collection.Effects
                .OrderBy(g => (g.Value ?? "").Length).ThenBy(g => g.Value))
            {
                string effects = group.Effects != null && group.Effects.Count > 0
                    ? string.Join("<br>", group.Effects.Select(fx => Enc(fx.DisplayLabel)))
                    : "<i>none</i>";
                sb.AppendLine("<tr>");
                sb.AppendLine($"  <td class=\"mono\">{Enc(group.Value)}</td>");
                sb.AppendLine($"  <td>{Enc(group.Lr ?? "Any")}</td>");
                sb.AppendLine($"  <td>{effects}</td>");
                sb.AppendLine("</tr>");
            }
            return sb.ToString();
        }

        private static string BuildPacks(CollectionFile collection)
        {
            var byId = new Dictionary<string, CollectionCard>();
            foreach (var c in collection.Cards)
                if (c.Id != null && !byId.ContainsKey(c.Id)) byId[c.Id] = c;

            var sb = new StringBuilder();
            foreach (var pack in collection.Packs)
            {
                int totalOdds = pack.Cards.Sum(p => p.Odds);
                var contents = pack.Cards.Select(p =>
                {
                    string label = byId.TryGetValue(p.Id, out var card)
                        ? Enc(card.DisplayLabel) : "<i>missing card</i>";
                    string pct = totalOdds > 0
                        ? $" — {100.0 * p.Odds / totalOdds:0.##}%" : "";
                    return label + pct;
                });

                sb.AppendLine("<tr>");
                sb.Append("  <td>");
                if (!string.IsNullOrEmpty(pack.Sprite))
                    sb.Append($"<img class=\"pack-sprite\" src=\"../{Enc(pack.Sprite)}\" alt=\"\" onerror=\"this.style.display='none'\"> ");
                sb.AppendLine($"{Enc(pack.Name)}</td>");
                sb.AppendLine($"  <td>{pack.CardsPerPack}</td>");
                sb.AppendLine($"  <td>{pack.ShineChance:0.00}%</td>");
                sb.AppendLine($"  <td>{string.Join("<br>", contents)}</td>");
                sb.AppendLine("</tr>");
            }
            return sb.ToString();
        }
    }
}
