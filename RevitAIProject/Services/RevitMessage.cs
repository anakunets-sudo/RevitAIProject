using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public struct RevitMessage
    {        
        public string Text { get; set; }
        public RevitMessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
