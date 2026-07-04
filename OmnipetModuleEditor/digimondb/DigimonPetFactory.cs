using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmnipetModuleEditor.Models;

namespace OmnipetModuleEditor.DigimonSync
{
    /// <summary>
    /// Builds new module pets from Digimon Database records: maps DB attribute
    /// names to the module's attribute codes and overlays a module's extra-data
    /// onto a pet.
    /// </summary>
    public static class DigimonPetFactory
    {
        /// <summary>
        /// Map the first known DB attribute name (Vaccine/Virus/Data) to the
        /// module attribute code (Va/Vi/Da). Free/Unknown/none -> "".
        /// </summary>
        public static string MapAttributeCode(IEnumerable<string> dbAttributes)
        {
            if (dbAttributes != null)
            {
                foreach (var a in dbAttributes)
                {
                    switch (a)
                    {
                        case "Vaccine": return "Va";
                        case "Virus": return "Vi";
                        case "Data": return "Da";
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Return a copy of <paramref name="pet"/> with the module's extra-data
        /// fields overlaid (info fields, attack sprites, evolve, ...). Fields the
        /// extra-data does not contain (name, stage, min_weight, attribute) are
        /// preserved from the input pet.
        /// </summary>
        public static Pet WithExtraData(Pet pet, JsonElement extra)
        {
            var node = JsonSerializer.SerializeToNode(pet) as JsonObject;
            if (node == null)
                return pet;
            foreach (var prop in extra.EnumerateObject())
                node[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
            return node.Deserialize<Pet>() ?? pet;
        }
    }
}
