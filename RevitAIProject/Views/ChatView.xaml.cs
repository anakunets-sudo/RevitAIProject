using Autodesk.Revit.UI;
using GalaSoft.MvvmLight.Ioc;
using RevitAIProject.Services;
using RevitAIProject.ViewModels;
using System;
using System.Windows.Controls;

namespace RevitAIProject.Views
{
    /// <summary>
    /// Логика взаимодействия для ChatView.xaml
    /// </summary>
    public partial class ChatView : Page, IDockablePaneProvider
    {
        public ChatView(UIControlledApplication a)
        {
            RevitTaskHandler handler = new RevitTaskHandler();
            IRevitApiService revitService = new RevitApiService(handler);
            IOllamaService ollamaService = new OllamaService();

            if (!SimpleIoc.Default.IsRegistered<ChatViewModel>())
            {
                SimpleIoc.Default.Register(() => new ChatViewModel(ollamaService, revitService));
            }

            InitializeComponent();

            //this.DataContext = SimpleIoc.Default.GetInstance<ChatViewModel>();
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
