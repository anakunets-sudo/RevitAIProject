using Autodesk.Revit.UI;
using GalaSoft.MvvmLight.Ioc;
using RevitAIProject.Services;
using RevitAIProject.ViewModels;
using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace RevitAIProject.Views
{
    /// <summary>
    /// Логика взаимодействия для ChatView.xaml
    /// </summary>
    public partial class ChatView : Page, IDockablePaneProvider, IUIDispatcherHelper
    {
        private readonly Dispatcher _uiDispatcher;

        public ChatView(UIControlledApplication a)
        {
            RevitTaskHandler handler = new RevitTaskHandler();
            IRevitApiService revitService = new RevitApiService(handler);
            IOllamaService ollamaService = new OllamaService();
            var voiceService = new VoiceService();

            if (!SimpleIoc.Default.IsRegistered<ChatViewModel>())
            {
                SimpleIoc.Default.Register(() => new ChatViewModel(ollamaService, revitService, voiceService, this));
            }

            InitializeComponent();

            //this.DataContext = SimpleIoc.Default.GetInstance<ChatViewModel>();

            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }


        public void Invoke(Action action)
        {
            // Проверяем, не закрывается ли приложение
            if (_uiDispatcher.HasShutdownStarted) return;

            // Используем Background приоритет для "живого ввода". 
            // Это позволяет Revit сначала обработать клики и отрисовку, 
            // а уже потом вписать текст в поле чата.
            _uiDispatcher.BeginInvoke(DispatcherPriority.Background, action);
        }

        public static DockablePaneId PaneId
        {
            get
            {
                return new DockablePaneId(new Guid("73BD63D2-D874-4ABA-B80B-6003950F8121"));
            }
        }

        public static string PaneName
        {
            get
            {
                return "AI Assistant";
            }
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
        }
    }
}
