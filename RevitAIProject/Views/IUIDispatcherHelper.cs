using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Views
{
    // Простой интерфейс для получения диспетчера
    public interface IUIDispatcherHelper
    {
        void Invoke(Action action);
    }
}
