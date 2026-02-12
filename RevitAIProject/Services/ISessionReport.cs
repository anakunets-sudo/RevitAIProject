using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface ISessionReport
    {
        // Список всех накопленных сообщений в текущей сессии
        IReadOnlyList<RevitMessage> Reports { get; }

        // Основной метод добавления рапорта от экшенов или запросов
        void Report(string message, RevitMessageType type);

        // Получение отфильтрованных сообщений специально для ИИ (AiReport)
        IEnumerable<string> GetAiMessages();
    }
}
