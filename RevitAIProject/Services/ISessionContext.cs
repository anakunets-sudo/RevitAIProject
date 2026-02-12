using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface ISessionContext: ISessionStorage, ISessionReport
    {     
        void Reset();
    }
}
