using RevitAIProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface IExperienceRepository
    {
        void Save(ExperienceRecord record);
        List<ExperienceRecord> GetLearningSet(int limit);
    }
}
