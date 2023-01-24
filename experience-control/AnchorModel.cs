using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Solipsist.ExperienceControl
{
    internal class AnchorModel
    {
        [Key]
        [JsonPropertyName("id")]
        public Guid? id { get; set; }
    }
}
