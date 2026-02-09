using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitAIProject.Services
{
    public interface IOllamaService
    {
        // Метод отправки запроса и получения ответа (асинхронно)
        Task<AiResponse> GetAiResponseAsync(string userMessage, CancellationToken ct);
    }
}
