using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Models
{
    /// <summary>
    /// Model for storing user feedback and AI response pair.
    /// </summary>
    public class ExperienceRecord
    {
        public string UserPrompt { get; set; }
        public object AiJson { get; set; }
        public int Rating { get; set; } // -2 to 2
        public DateTime Timestamp { get; set; }
    }
}
