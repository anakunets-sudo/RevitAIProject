using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public enum RevitMessageType
    {
        Info,   // Обычные уведомления от Revit ("Система: ...")
        Error,    // Ошибки ("Ошибка: ...")
        Ai,       // Ответы от нейросети ("AI: ...")
        User,      // Сообщения пользователя ("Вы: ...")
        AiReport,
        Warning
    }
}
