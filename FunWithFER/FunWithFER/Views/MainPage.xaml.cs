using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using FunWithFER.Helpers;
using FunWithFER.MLParsers;
using FunWithFER.Models;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
namespace FunWithFER.Views
{
    public sealed partial class MainPage : Page
    {
        private string tinyYoloOnyxFileName = "tiny-yolov2-1.2.onnx";
        private string ferOnyxFileName = "emotion_ferplus-1.2.onnx";

        private readonly SolidColorBrush lineBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
        private readonly SolidColorBrush fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
        private readonly double lineThickness = 2.0;

        private MediaCapture mediaCapture;
        private VideoEncodingProperties videoProperties;
        private ThreadPoolTimer frameProcessingTimer;
        private readonly SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1);

        private ImageVariableDescriptorPreview inputImageDescription;
        private TensorVariableDescriptorPreview outputTensorDescription;
        private LearningModelPreview model = null;

        private IList<BoundingBox> boxes = new List<BoundingBox>();
        private readonly TinyYoloParser parser = new TinyYoloParser();

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await LoadModelAsync();
        }

        #region Event handlers
        
        private async void EvaluateImageButton_Clicked(object sender, RoutedEventArgs e)
        {
            EvaluateImageButton.IsEnabled = false;

            try
            {
                // Load the model
                await Task.Run(async () => await LoadModelAsync());

                // Trigger file picker to select an image file
                var fileOpenPicker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                    ViewMode = PickerViewMode.Thumbnail
                };

                fileOpenPicker.FileTypeFilter.Add(".jpg");
                fileOpenPicker.FileTypeFilter.Add(".png");

                var selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();

                SoftwareBitmap softwareBitmap;

                using (IRandomAccessStream stream = await selectedStorageFile.OpenAsync(FileAccessMode.Read))
                {
                    // Create the decoder from the stream 
                    var decoder = await BitmapDecoder.CreateAsync(stream);

                    // Get the SoftwareBitmap representation of the file in BGRA8 format
                    softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                // Encapsulate the image within a VideoFrame to be bound and evaluated
                var inputImage = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);

                await Task.Run(async () =>
                {
                    // Evaluate the image
                    await EvaluateVideoFrameWithYoloAsync(inputImage);
                });

                await DrawOverlays(inputImage);
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"error: {ex.Message}");
                EvaluateImageButton.IsEnabled = true;
            }
        }
        
        private async void EvaluateVideoButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (mediaCapture == null || mediaCapture.CameraStreamState != CameraStreamState.Streaming)
            {
                await StartVideoDeviceAsync();
            }
            else
            {
                await StopVideoDeviceAsync();
            }
        }

        private async void OnDeviceToggleToggled(object sender, RoutedEventArgs e)
        {
            await LoadModelAsync(DeviceToggle.IsOn);
        }
        
        private async Task StartVideoDeviceAsync()
        {
            try
            {
                if (mediaCapture == null || mediaCapture.CameraStreamState == CameraStreamState.Shutdown || mediaCapture.CameraStreamState == CameraStreamState.NotStreaming)
                {
                    mediaCapture?.Dispose();
                    
                    var selectedCamera = await CameraUtilities.FindBestCameraAsync(DeviceClass.VideoCapture);
                    
                    if (selectedCamera == null)
                    {
                        await new MessageDialog("There are no cameras connected, please connect a camera and try again.").ShowAsync();

                        mediaCapture?.Dispose();
                        mediaCapture = null;

                        return;
                    }
                    
                    mediaCapture = new MediaCapture();

                    // put reference in App so that it can be disposed if app is suspended
                    App.MediaCaptureManager = mediaCapture;

                    await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings { VideoDeviceId = selectedCamera.Id });

                    WebCamCaptureElement.Source = mediaCapture;
                }

                if (mediaCapture.CameraStreamState == CameraStreamState.NotStreaming)
                {
                    // Frame lock timer logic credit: Rene Shulte, see https://github.com/sevans4067/WinMl-TinyYOLO/blob/master/TinyYOLO/MainPage.xaml.cs

                    if (frameProcessingTimer != null)
                    {
                        frameProcessingTimer.Cancel();
                        frameProcessingSemaphore.Release();
                    }
                    
                    frameProcessingTimer = ThreadPoolTimer.CreatePeriodicTimer(ProcessCurrentVideoFrame, TimeSpan.FromMilliseconds(66)); // 1000 (ms) / 15 (fps) == 66 (ms)

                    videoProperties = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

                    await mediaCapture.StartPreviewAsync();
                    
                    App.GlobalDisplayRequest.RequestActive();

                    WebCamCaptureElement.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"StartVideoDeviceAsync Error: {ex.Message}");
            }
        }

        public async Task StopVideoDeviceAsync()
        {
            try
            {
                frameProcessingTimer?.Cancel();

                if (mediaCapture != null && mediaCapture.CameraStreamState != CameraStreamState.Shutdown)
                {
                    await mediaCapture.StopPreviewAsync();

                    App.GlobalDisplayRequest?.RequestRelease();
                    
                    WebCamCaptureElement.Source = null;
                    mediaCapture.Dispose();
                    mediaCapture = null;

                    WebCamCaptureElement.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"StopVideoDeviceAsync Error: {ex.Message}");
            }
        }
        
        #endregion


        #region WinML init and evaluation

        private async Task LoadModelAsync(bool isGpu = true)
        {
            if (model != null)
                return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"Loading { tinyYoloOnyxFileName } ... patience ");

            try
            {
                // Load TinyYoloModel
                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/{ tinyYoloOnyxFileName }"));

                model = await LearningModelPreview.LoadModelFromStorageFileAsync(modelFile);

                model.InferencingOptions.ReclaimMemoryAfterEvaluation = true;

                model.InferencingOptions.PreferredDeviceKind = isGpu
                    ? LearningModelDeviceKindPreview.LearningDeviceGpu
                    : LearningModelDeviceKindPreview.LearningDeviceCpu;

                // Retrieve model input and output variable descriptions (we already know the model takes an image in and outputs a tensor)
                var inputFeatures = model.Description.InputFeatures.ToList();
                var outputFeatures = model.Description.OutputFeatures.ToList();

                inputImageDescription = inputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Image) as ImageVariableDescriptorPreview;

                outputTensorDescription = outputFeatures.FirstOrDefault(feature => feature.ModelFeatureKind == LearningModelFeatureKindPreview.Tensor) as TensorVariableDescriptorPreview;

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"Loaded { tinyYoloOnyxFileName }. Press the camera button to start the webcam...");

            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"LoadModelAsync Error: {ex.Message}");
                model = null;
            }
        }

        private async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {
            if (mediaCapture.CameraStreamState != CameraStreamState.Streaming || !frameProcessingSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                //var props = mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;
                //VideoFrame previewFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)props.Width, (int)props.Height);
                
                var previewFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)videoProperties.Width, (int)videoProperties.Height);

                await mediaCapture.GetPreviewFrameAsync(previewFrame);
                await EvaluateVideoFrameWithYoloAsync(previewFrame);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await DrawOverlays(previewFrame);
                    previewFrame.Dispose();
                });

            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"ProcessCurrentVideoFrame Error: {ex.Message}");
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }
        }
        
        private async Task EvaluateVideoFrameWithYoloAsync(VideoFrame inputFrame)
        {
            if (inputFrame != null)
            {
                try
                {
                    // Create bindings for the input and output buffer
                    var binding = new LearningModelBindingPreview(model);

                    // R4 WinML does needs the output pre-allocated for multi-dimensional tensors
                    var outputArray = new List<float>();
                    outputArray.AddRange(new float[21125]);  // Total size of TinyYOLO output

                    binding.Bind(inputImageDescription.Name, inputFrame);
                    binding.Bind(outputTensorDescription.Name, outputArray);

                    // Process the frame with the model
                    var stopwatch = Stopwatch.StartNew();

                    //var results = await model.EvaluateAsync(binding, "TinyYOLO");

                    var results = await model.EvaluateAsync(binding, "TinyYOLOv2");

                    stopwatch.Stop();

                    var resultProbabilities = results.Outputs[outputTensorDescription.Name] as List<float>;

                    // Use out helper to parse to the YOLO outputs into bounding boxes with labels
                    boxes = parser.ParseOutputs(resultProbabilities.ToArray(), .3F);

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Duration.Text = $"{1000f / stopwatch.ElapsedMilliseconds,4:f1} fps";
                        StatusBlock.Text = "TinyYoloModel Evaluation Completed";
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"EvaluateVideoFrameWithYoloAsync Error: {ex.Message}");
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => EvaluateImageButton.IsEnabled = true);
            }
        }

        private async Task EvaluateVideoFrameWithFacialEmotionAsync(VideoFrame inputFrame)
        {
            if (inputFrame != null)
            {
                try
                {
                    // Create bindings for the input and output buffer
                    var binding = new LearningModelBindingPreview(model);

                    // R4 WinML does needs the output pre-allocated for multi-dimensional tensors
                    var outputArray = new List<float>();
                    outputArray.AddRange(new float[21125]);  // Total size of TinyYOLO output

                    binding.Bind(inputImageDescription.Name, inputFrame);
                    binding.Bind(outputTensorDescription.Name, outputArray);

                    // Process the frame with the model
                    var stopwatch = Stopwatch.StartNew();
                    
                    var results = await model.EvaluateAsync(binding, "FER");

                    stopwatch.Stop();

                    var resultProbabilities = results.Outputs[outputTensorDescription.Name] as List<float>;

                    // Use out helper to parse to the YOLO outputs into bounding boxes with labels
                    boxes = parser.ParseOutputs(resultProbabilities.ToArray(), .3F);

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Duration.Text = $"{1000f / stopwatch.ElapsedMilliseconds,4:f1} fps";
                        StatusBlock.Text = "FacialEmotion Evaluation Completed";
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"EvaluateVideoFrameWithFacialEmotionAsync Error: {ex.Message}");
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => EvaluateImageButton.IsEnabled = true);
            }
        }

        #endregion

        #region UI drawing

        private async Task DrawOverlays(VideoFrame inputImage)
        {
            OverlayCanvas.Children.Clear();

            // Render output
            if (boxes.Count > 0)
            {
                // Remove overalapping and low confidence bounding boxes
                var filteredBoxes = parser.NonMaxSuppress(boxes, 5, .5F);

                foreach (var box in filteredBoxes)
                    await DrawYoloBoundingBoxAsync(inputImage.SoftwareBitmap, box);
            }
        }

        private async Task DrawYoloBoundingBoxAsync(SoftwareBitmap inputImage, BoundingBox box)
        {
            try
            {
                // Scale is set to stretched 416x416 - Clip bounding boxes to image area
                var x = (uint)Math.Max(box.X, 0);
                var y = (uint)Math.Max(box.Y, 0);
                var w = (uint)Math.Min(OverlayCanvas.Width - x, box.Width);
                var h = (uint)Math.Min(OverlayCanvas.Height - y, box.Height);

                var brush = new ImageBrush();

                var bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(inputImage);

                brush.ImageSource = bitmapSource;
                brush.Stretch = Stretch.Fill;

                OverlayCanvas.Background = brush;


                OverlayCanvas.Children.Add(new Rectangle
                {
                    Width = 134,
                    Height = 29,
                    Fill = lineBrush,
                    Margin = new Thickness(x, y, 0, 0)
                });

                OverlayCanvas.Children.Add(new Rectangle
                {
                    Tag = box,
                    Width = w,
                    Height = h,
                    Fill = fillBrush,
                    Stroke = lineBrush,
                    StrokeThickness = lineThickness,
                    Margin = new Thickness(x, y, 0, 0)
                });

                OverlayCanvas.Children.Add(new TextBlock
                {
                    Margin = new Thickness(x + 4, y + 4, 0, 0),
                    Text = $"{box.Label} ({Math.Round(box.Confidence, 4).ToString(CultureInfo.InvariantCulture)})",
                    FontWeight = FontWeights.Bold,
                    Width = 126,
                    Height = 21,
                    HorizontalTextAlignment = TextAlignment.Center
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusBlock.Text = $"DrawYoloBoundingBoxAsync Error: {ex.Message}");
            }
        }
        
        #endregion
    }
}
