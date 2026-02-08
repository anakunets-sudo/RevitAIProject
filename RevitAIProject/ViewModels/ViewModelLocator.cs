using CommonServiceLocator;
using GalaSoft.MvvmLight;

namespace RevitAIProject.ViewModels
{
    public class ViewModelLocator
    {
        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => GalaSoft.MvvmLight.Ioc.SimpleIoc.Default);

            if (ViewModelBase.IsInDesignModeStatic)
            {                
            }
            else
            {
            }
        }

        public ViewModels.ChatViewModel ChatViewModel => ServiceLocator.Current.GetInstance<ViewModels.ChatViewModel>();
    }
}
