using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic
{
    public interface IRevitLogic
    {
        string Name { get; }
        void Execute(RevitAIProject.Services.IRevitApiService apiService);
    }
}
