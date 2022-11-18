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
        public ExperienceMetadata(string ownerID, string name)
        {
            this.ownerID = ownerID;
            this.name = name;
            this.state = ExperienceState.Stopped;
        }

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