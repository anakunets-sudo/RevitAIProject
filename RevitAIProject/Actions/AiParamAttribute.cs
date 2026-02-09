using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Actions
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AiParamAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; set; } // Добавляем свойство для описания

        public AiParamAttribute(string name) => Name = name;
    }
}
