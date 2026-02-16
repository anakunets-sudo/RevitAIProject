using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RevitAIProject.Logic.Actions
{
    [AiParam("dynamic_code", Description = "Executes custom C# logic if no specialized tool is available.")]
    public class DynamicCodeAction : BaseRevitAction
    {
        [AiParam("csharp_code", Description = "Clean C# 7.3 code block for Revit API.")]
        public string CSharpCode { get; set; }

        protected override void Execute(IRevitContext context)
        {
            try
            {
                var compiler = new RevitCompilerService();                

                // Передаем сырой код и параметры
                var dynamicAction = compiler.Compile(CSharpCode, TargetAiName, AssignAiName);

                ValidateCodeSafety(CSharpCode);

                using (Transaction t = new Transaction(context.UIDoc.Document, "AI Dynamic Action"))
                {
                    t.Start();

                    dynamicAction(context);

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                // 1. Извлекаем реальную ошибку из недр Reflection
                Exception realException = ex;
                if (ex is TargetInvocationException && ex.InnerException != null)
                    realException = ex.InnerException;

                // 2. Формируем понятный лог
                string errorType = realException.GetType().Name;
                string errorMessage = realException.Message;

                // Пытаемся выудить номер строки из StackTrace (благодаря #line 1 он будет верным)
                var stackTrace = new StackTrace(realException, true);
                var frame = stackTrace.GetFrames()?.FirstOrDefault(f => f.GetFileName() == "AI_GENERATED_CODE");
                int line = frame?.GetFileLineNumber() ?? 0;

                string finalReport = $"[Dynamic Error] {errorType} at line {line}: {errorMessage}";

                // Отправляем отчет в систему логирования ИИ
                Report(finalReport, RevitMessageType.Error);
            }
        }

        private void ValidateCodeSafety(string code)
        {
            string[] forbidden = { "System.IO", "System.Net", "Process.", "DllImport", "Reflection" };
            foreach (var word in forbidden)
                if (code.Contains(word))
                    throw new Exception($"Security: Forbidden access to '{word}' detected.");
        }
    }
}
