using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Solipsist.ExperienceControl
{
    // Must be kept in sync with the Unity model!
    internal class AnchorModel
    {
        [Key]
        [JsonPropertyName("id")]
        public Guid? id { get; set; }

        [JsonPropertyName("data")]
        public string data { get; set; }
    }
}
