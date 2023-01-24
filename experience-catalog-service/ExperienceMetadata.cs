using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Solipsist.ExperienceCatalog
{
    public enum ExperienceState
    {
        Stopped,
        Starting,
        Running,
        Stopping
    }

    public class ExperienceMetadata
    {
        [Key]
        [JsonPropertyName("id")]
        public Guid? id { get; set; }

        [JsonPropertyName("ownerID")]
        public string ownerID { get; set; }

        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("state")]
        public ExperienceState state { get; set; }
    }
}