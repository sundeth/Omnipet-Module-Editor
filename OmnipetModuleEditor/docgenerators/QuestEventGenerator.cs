using OmnipetModuleEditor.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OmnipetModuleEditor.docgenerators
{
    internal class QuestEventGenerator
    {
        public static void GenerateQuestEventPage(string docPath, string modulePath)
        {
            string template = GeneratorUtils.GetTemplateContent("questevents.html");
            
            var quests = LoadQuests(modulePath);
            var events = LoadEvents(modulePath);

            var questsContent = GenerateQuestsTable(quests);
            var eventsContent = GenerateEventsTable(events);

            string content = template
                .Replace("#QUESTSDATA", questsContent)
                .Replace("#EVENTSDATA", eventsContent)
                .Replace("#EVENTEXTRAS", GenerateEventExtras(modulePath));

            File.WriteAllText(Path.Combine(docPath, "questevents.html"), content);
        }

        private static List<Quest> LoadQuests(string modulePath)
        {
            if (string.IsNullOrEmpty(modulePath))
                return new List<Quest>();

            string questPath = Path.Combine(modulePath, "quests.json");
            if (!File.Exists(questPath))
                return new List<Quest>();

            try
            {
                string json = File.ReadAllText(questPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("quests", out var questsElement))
                    {
                        return JsonSerializer.Deserialize<List<Quest>>(questsElement.GetRawText()) ?? new List<Quest>();
                    }
                }
            }
            catch { }

            return new List<Quest>();
        }

        private static List<Event> LoadEvents(string modulePath)
        {
            if (string.IsNullOrEmpty(modulePath))
                return new List<Event>();

            string eventPath = Path.Combine(modulePath, "events.json");
            if (!File.Exists(eventPath))
                return new List<Event>();

            try
            {
                string json = File.ReadAllText(eventPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("events", out var eventsElement))
                    {
                        return JsonSerializer.Deserialize<List<Event>>(eventsElement.GetRawText()) ?? new List<Event>();
                    }
                }
            }
            catch { }

            return new List<Event>();
        }

        private static string GenerateQuestsTable(List<Quest> quests)
        {
            var sb = new StringBuilder();

            if (quests != null && quests.Count > 0)
            {
                foreach (var quest in quests)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"  <td><strong>{quest.Name ?? "Unknown Quest"}</strong></td>");
                    sb.AppendLine($"  <td>{quest.QuestType}</td>");
                    
                    // Target Amount Range
                    string targetAmount = "1";
                    if (quest.TargetAmountRange?.Length >= 2)
                    {
                        if (quest.TargetAmountRange[0] == quest.TargetAmountRange[1])
                            targetAmount = quest.TargetAmountRange[0].ToString();
                        else
                            targetAmount = $"{quest.TargetAmountRange[0]}-{quest.TargetAmountRange[1]}";
                    }
                    if (quest.QuestType == QuestType.Daily_Gift)
                        targetAmount = "Nothing (a gift every day)";
                    sb.AppendLine($"  <td>{targetAmount}</td>");
                    
                    sb.AppendLine($"  <td>{quest.RewardType}</td>");
                    sb.AppendLine($"  <td>{quest.RewardValue ?? ""}</td>");
                    sb.AppendLine($"  <td>{quest.RewardQuantity}</td>");
                    sb.AppendLine("</tr>");
                }
            }

            return sb.ToString();
        }

        private static string GenerateEventsTable(List<Event> events)
        {
            var sb = new StringBuilder();

            if (events != null && events.Count > 0)
            {
                foreach (var eventItem in events)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"  <td><strong>{eventItem.Name ?? "Unknown Event"}</strong></td>");
                    
                    // Global
                    string globalClass = eventItem.Global ? "boolean-true" : "boolean-false";
                    string globalText = eventItem.Global ? "Yes" : "No";
                    sb.AppendLine($"  <td class=\"{globalClass}\">{globalText}</td>");

                    string trigger = string.IsNullOrEmpty(eventItem.Trigger) ? "random" : eventItem.Trigger;
                    if (trigger == "adventure" || trigger == "area_clear" || trigger == "connection")
                        trigger += $" ({(string.IsNullOrEmpty(eventItem.Outcome) ? "win" : eventItem.Outcome)})";
                    sb.AppendLine($"  <td>{Esc(trigger)}</td>");
                    sb.AppendLine($"  <td>{eventItem.Type}</td>");
                    sb.AppendLine($"  <td>{eventItem.ChancePercent:F1}%</td>");
                    sb.AppendLine($"  <td>{eventItem.Area?.ToString() ?? "-"}</td>");
                    sb.AppendLine($"  <td>{eventItem.Round?.ToString() ?? "-"}</td>");
                    string item = eventItem.Item ?? "";
                    if (string.IsNullOrEmpty(item) && eventItem.ItemPool != null && eventItem.ItemPool.Count > 0)
                        item = $"one of {eventItem.ItemPool.Count}";
                    sb.AppendLine($"  <td>{Esc(item)}</td>");
                    sb.AppendLine($"  <td>{eventItem.ItemQuantity?.ToString() ?? "-"}</td>");
                    sb.AppendLine($"  <td>{Esc(Details(eventItem))}</td>");
                    sb.AppendLine("</tr>");
                }
            }

            return sb.ToString();
        }

        private static string Esc(string text) => System.Net.WebUtility.HtmlEncode(text ?? "");

        /// <summary>What a row's newer fields say, in a few words.</summary>
        private static string Details(Event ev)
        {
            var parts = new List<string>();
            if (ev.Versions != null && ev.Versions.Count > 0)
                parts.Add("versions " + string.Join(", ", ev.Versions));
            if (ev.TriggerAreas != null && ev.TriggerAreas.Count > 0)
                parts.Add("in area " + string.Join(", ", ev.TriggerAreas));
            if (ev.UntilFriend)
                parts.Add("until it is a Friend");
            if (ev.OpponentDeviceVersion.HasValue)
                parts.Add(ev.OpponentDeviceVersion.Value == -1 ? "against any other device"
                          : "against device " + ev.OpponentDeviceVersion.Value);
            if (!string.IsNullOrEmpty(ev.Protocol))
                parts.Add("on the " + ev.Protocol + " format");
            if (ev.Type == EventType.ShopAccess)
                parts.Add($"opens {ev.Shop}" + (ev.AccessMinutes > 0 ? $" for {ev.AccessMinutes} min"
                                                : ev.AccessMinutes == -1 ? " for one visit" : " for good"));
            if (!string.IsNullOrEmpty(ev.Draw))
                parts.Add($"one roll with the other \"{ev.Draw}\" events");
            if (ev.Type == EventType.BattleRun)
                parts.Add("every round of the area; reward after " + (ev.RewardRound > 0 ? "round " + ev.RewardRound : "the last"));
            if (ev.ItemPool != null && ev.ItemPool.Count > 0
                && (ev.Type == EventType.ItemPackage || ev.Type == EventType.BattleRun))
                parts.Add("pays one at random of: " + string.Join(", ", ev.ItemPool));
            if (ev.Type == EventType.PoolBattle && ev.Pools != null)
                parts.Add($"{ev.Pools.Count} pools by {ev.PoolField}");
            if (ev.Type == EventType.CardBattle)
            {
                parts.Add("a character of the pet's own card, by its battle chances");
                if (ev.ItemSequence != null && ev.ItemSequence.Count > 0)
                {
                    var given = ev.ItemSequence.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n)
                        .Select(g => $"{g.Key} {g.Count()}");
                    parts.Add($"wins pay in a {ev.ItemSequence.Count}-step order: " + string.Join(", ", given)
                              + $", nothing {ev.ItemSequence.Count(n => string.IsNullOrEmpty(n))}");
                }
            }
            if (ev.DelayMinutes != null && ev.DelayMinutes.Length > 0)
                parts.Add($"{string.Join("-", ev.DelayMinutes)} min later");
            return string.Join("; ", parts);
        }

        /// <summary>events.json's Connect Points and schedule, as sections
        /// of their own; "" when the module has neither.</summary>
        private static string GenerateEventExtras(string modulePath)
        {
            string path = Path.Combine(modulePath ?? "", "events.json");
            if (!File.Exists(path))
                return "";
            var sb = new StringBuilder();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        return "";
                    if (root.TryGetProperty("connect_points", out var cpElement))
                    {
                        var cp = JsonSerializer.Deserialize<ConnectPoints>(cpElement.GetRawText());
                        if (cp != null && cp.Cap > 0)
                        {
                            sb.AppendLine("<div class=\"section\"><h2>Connect Points</h2>");
                            var counted = (cp.Battles ?? new List<string>()).Select(b => (b ?? "").ToLowerInvariant()).ToList();
                            bool adventure = counted.Contains("adventure");
                            bool connection = counted.Count == 0 || counted.Contains("connection") || !adventure;
                            string which = adventure && connection ? "Adventure battles and battles with another device or pet"
                                         : adventure ? "Adventure battles" : "Battles with another device or pet";
                            if (cp.Cap == 1)
                                sb.AppendLine($"<p>{which} roll for an item: {(cp.Win > 0 && cp.Loss > 0 ? "every battle" : cp.Win > 0 ? "every battle won" : "every battle lost")} draws one of the rewards below, by its chance; \"nothing\" gives no item.</p>");
                            else
                                sb.AppendLine($"<p>{which} give {cp.Win} for a win and {cp.Loss} for a loss. At {cp.Cap} the Digimon gets one of the rewards below, each listed one equally likely, and starts again from 0. Kept through evolution.</p>");
                            sb.AppendLine("<table><thead><tr><th>Version</th><th>Reward</th><th>Chance</th></tr></thead><tbody>");
                            foreach (var group in cp.Rewards.GroupBy(r => r.Version))
                            {
                                int total = group.Count();
                                foreach (var item in group.GroupBy(r => r.Item))
                                    sb.AppendLine($"<tr><td>{(group.Key.HasValue ? group.Key.Value.ToString() : "all")}</td><td>{(string.IsNullOrEmpty(item.Key) ? "nothing" : Esc(item.Key))}</td><td>{100.0 * item.Count() / total:0.#}%</td></tr>");
                            }
                            sb.AppendLine("</tbody></table></div>");
                        }
                    }
                    if (root.TryGetProperty("schedule", out var scheduleElement))
                    {
                        var schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleElement.GetRawText());
                        if (schedule != null && schedule.Count > 0)
                        {
                            sb.AppendLine("<div class=\"section\"><h2>Encounter Schedule</h2>");
                            sb.AppendLine("<p>The times a scheduled event can fire for a Digimon of each encounter time group.</p>");
                            sb.AppendLine("<table><thead><tr><th>Time Group</th><th>Times</th></tr></thead><tbody>");
                            foreach (var group in schedule)
                                sb.AppendLine($"<tr><td>{Esc(group.Key)}</td><td>{Esc(string.Join(", ", group.Value))}</td></tr>");
                            sb.AppendLine("</tbody></table></div>");
                        }
                    }
                }
            }
            catch { }
            return sb.ToString();
        }
    }
}