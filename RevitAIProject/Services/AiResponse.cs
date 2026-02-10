using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{   
    public class AiResponse
    {
        public string Message { get; set; }

        // Список действий, которые ИИ предлагает выполнить
        public List<Logic.Actions.IRevitAction> Actions { get; set; } = new List<Logic.Actions.IRevitAction>();
    }
}
