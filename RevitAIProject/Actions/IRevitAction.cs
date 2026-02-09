using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface IRevitAction
    {
        string ActionName { get; }
        void Execute(IRevitApiService apiService);
    }
}
