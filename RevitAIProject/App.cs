using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitAIProject
{
    public class App : IExternalApplication
    {
        public static ExternalEvent ExternalEvent { get; private set; }
        // Статическая ссылка для доступа из любой точки плагина
        public static UIApplication UiApp;

        public Result OnStartup(UIControlledApplication a)
        {
            // Регистрация панели
            var view = new Views.ChatView(a);
            var paneId = Views.ChatView.PaneId;
            a.RegisterDockablePane(paneId, "AI Roof Assistant", view);

            // 3. Создаем кнопку на ленте
            CreateRibbon(a);

            // Подписываемся на событие Idling, чтобы поймать момент появления UIApplication
            a.Idling += OnIdling;

            return Result.Succeeded;
        }

        private void CreateRibbon(UIControlledApplication a)
        {
            string tabName = "AI Tools";
            try { a.CreateRibbonTab(tabName); } catch { } // Создаем вкладку, если её нет

            RibbonPanel panel = a.CreateRibbonPanel(tabName, "Assistant");

            // Путь к текущей DLL
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            // Создаем кнопку, которая вызывает наш класс ShowAiPane (из Command.cs)
            PushButtonData btnData = new PushButtonData(
                "ShowAiAssistant",
                "Open AI\nChat",
                assemblyPath,
                "RevitAIProject.ShowAiPane" // Полное имя класса с пространством имен!
            );

            PushButton btn = panel.AddItem(btnData) as PushButton;
            btn.ToolTip = "Открыть панель управления ИИ";

            // Можно добавить иконку (32x32)
            // btn.LargeImage = new BitmapImage(new Uri("pack://application:,,,/YourAssembly;component/Resources/ai_icon.png"));
        }

        private void OnIdling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            // Как только Revit стал доступен, сохраняем ссылку на приложение
            if (UiApp == null && sender is UIApplication app)
            {
                UiApp = app;
            }
        }

        public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
    }
}
