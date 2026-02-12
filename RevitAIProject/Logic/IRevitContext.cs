using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAIProject.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAIProject.Logic
{
    /// <summary>
    /// РОЛЬ: Предоставляет классам безопасный доступ к UIApplication UIApp и IDocument UIDoc для работы с логикой Revit API
    /// ВХОД: Получает UIApplication, UIDocument и SessionContext у классов которые реализуют этот интерфейс.
    /// ВЫХОД: Использует ISessionStorage для хранения и обмена данными в сессии работы ИИ
    /// </summary>
    public interface IRevitContext
    {
        UIApplication UIApp { get; }
        UIDocument UIDoc { get; }
        ISessionStorage Storage { get; }
        //void Report(string message, RevitMessageType messageType);
    }
}
