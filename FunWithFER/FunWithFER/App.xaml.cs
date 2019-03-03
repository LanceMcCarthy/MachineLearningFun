using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Media.Capture;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using FunWithFER.Views;

namespace FunWithFER
{
    sealed partial class App : Application
    {
        // This is needed so that we can dispose the camera if the app get suspended
        public static MediaCapture MediaCaptureManager { get; set; }

        // This is used to prevent the screen from locking while the camera is active
        private static DisplayRequest _globalDisplayRequest;
        public static DisplayRequest GlobalDisplayRequest
        {
            get => _globalDisplayRequest ?? (_globalDisplayRequest = new DisplayRequest());
            set => _globalDisplayRequest = value;
        }

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }
        
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            
            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }
                
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    //rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    rootFrame.Navigate(typeof(VideoPage), e.Arguments);
                }

                Window.Current.Activate();
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
        
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            GlobalDisplayRequest?.RequestRelease();
            MediaCaptureManager?.Dispose();

            deferral.Complete();
        }
    }
}
