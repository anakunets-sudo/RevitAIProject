using RevitAIProject.Logic;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    /// <summary>
    /// Response from AI containing natural language message, 
    /// executable actions, and the raw JSON string for learning.
    /// </summary>
    public class AiResponse
    {
        /// <summary>
        /// Natural language message for the user.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// List of executable Revit logic objects.
        /// </summary>
        public List<IRevitLogic> Actions { get; set; } = new List<IRevitLogic>();

        /// <summary>
        /// The original JSON string from AI (used for training/feedback).
        /// </summary>
        public string RawJson { get; set; }
    }
}
