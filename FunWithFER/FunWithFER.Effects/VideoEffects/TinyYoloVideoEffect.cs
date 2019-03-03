using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using FunWithFER.Effects.Helpers;
using FunWithFER.Effects.MLParsers;
using FunWithFER.Effects.Models;

namespace FunWithFER.Effects.VideoEffects
{
    public sealed class TinyYoloVideoEffect : IBasicVideoEffect
    {
        // ** Fields ** //

        // Video Effect Fields
        private VideoEncodingProperties currentEncodingProperties;
        private CanvasDevice canvasDevice;
        private IPropertySet currentConfiguration;

        // WinML Fields
        private LearningModelDeviceKind detectedDeviceKind;
        private LearningModel model;
        private LearningModelBinding binding;
        private LearningModelSession session;
        
        private TinyYoloParser parser;
        private IList<BoundingBox> filteredBoxes;

        // General
        private int runCount = 0;
        private bool modelCreated;
        private readonly TimeSpan poolTimerInterval = TimeSpan.FromSeconds(1);
        private ThreadPoolTimer frameProcessingTimer;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private VideoFrame videoFrameToProcess;

        // ** Properties ** //

        /// <summary>
        /// The path to the ONYX model file, the default is TinyYolo2-1.2 shipped with the effect.
        /// </summary>
        public Uri ModelUri { get; set; } = new Uri("ms-appx:///Assets/tiny-yolov2-1.2.onnx");
        
        // ** Methods ** //

        // This is run for every video frame passed in the media pipeline (MediaPlayer, MediaCapture, etc)
        public void ProcessFrame(ProcessVideoFrameContext context)
        {
            videoFrameToProcess = VideoFrame.CreateWithDirect3D11Surface(context.InputFrame.Direct3DSurface);
            
            // ********** Draw Bounding Boxes with Win2D ********** //

            // Use Direct3DSurface if using GPU memory
            if (context.InputFrame.Direct3DSurface != null)
            {
                using (var inputBitmap = CanvasBitmap.CreateFromDirect3D11Surface(canvasDevice, context.InputFrame.Direct3DSurface))
                using (var renderTarget = CanvasRenderTarget.CreateFromDirect3D11Surface(canvasDevice, context.OutputFrame.Direct3DSurface))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(inputBitmap);

                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(renderTarget.Bounds.Width - x, box.Width);
                        var h = (uint)Math.Min(renderTarget.Bounds.Height - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }

                return;
            }

            // Use SoftwareBitmap if using CPU memory
            if (context.InputFrame.SoftwareBitmap != null)
            {
                // InputFrame's pixels
                byte[] inputFrameBytes = new byte[4 * context.InputFrame.SoftwareBitmap.PixelWidth * context.InputFrame.SoftwareBitmap.PixelHeight];
                context.InputFrame.SoftwareBitmap.CopyToBuffer(inputFrameBytes.AsBuffer());

                using (var inputBitmap = CanvasBitmap.CreateFromBytes(canvasDevice, inputFrameBytes, context.InputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight, context.InputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat()))
                using (var renderTarget = new CanvasRenderTarget(canvasDevice, context.OutputFrame.SoftwareBitmap.PixelWidth, context.InputFrame.SoftwareBitmap.PixelHeight, (float)context.OutputFrame.SoftwareBitmap.DpiX, context.OutputFrame.SoftwareBitmap.BitmapPixelFormat.ToDirectXPixelFormat(), CanvasAlphaMode.Premultiplied))
                using (var ds = renderTarget.CreateDrawingSession())
                {
                    ds.DrawImage(inputBitmap);

                    foreach (var box in filteredBoxes)
                    {
                        var x = (uint)Math.Max(box.X, 0);
                        var y = (uint)Math.Max(box.Y, 0);
                        var w = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelWidth - x, box.Width);
                        var h = (uint)Math.Min(context.OutputFrame.SoftwareBitmap.PixelHeight - y, box.Height);

                        // Draw the Text 10px above the top of the bounding box
                        ds.DrawText(box.Label, x, y - 10, Colors.Yellow);
                        ds.DrawRectangle(new Rect(x, y, w, h), new CanvasSolidColorBrush(canvasDevice, Colors.Yellow), 2f);
                    }
                }
            }
        }

        // This method is executed by the ThreadPoolTimer, it performs the evaluation on a copy of the VideoFrame
        private async void EvaluateVideoFrame(ThreadPoolTimer timer)
        {
            if (semaphore == null)
            {
                return;
            }

            if (!modelCreated)
            {
                Debug.WriteLine($"EvaluateVideoFrame Skipped - LearningModel Not Ready.");
                return;
            }

            // If a lock is being held, or WinML isn't fully initialized, return
            if (!semaphore.Wait(0))
            {
                Debug.WriteLine($"EvaluateVideoFrame Skipped - Waiting Semaphore Access");
                return;
            }

            if (session == null)
            {
                try
                {
                    Debug.WriteLine($"Attempting to create LearningModelSession using DeviceKind: {detectedDeviceKind}");

                    session = new LearningModelSession(model, new LearningModelDevice(detectedDeviceKind));

                    Debug.WriteLine($"LearningModelSession successfully created.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error Creating Session: {ex.Message}");
                    model = null;
                    return;
                }
            }

            Debug.WriteLine($"*************** Evaluating Video Frame [START] ***************");

            try
            {
                using (videoFrameToProcess)
                {
                    // ************ WinML Evaluate Frame ************ //
                    
                    Debug.WriteLine($"VideoFrame RelativeTime: {videoFrameToProcess.RelativeTime?.Seconds}s");
                    
                    // Create a binding object from the session
                    binding = new LearningModelBinding(session);

                    // Create an image tensor from a video frame
                    var image = ImageFeatureValue.CreateFromVideoFrame(videoFrameToProcess);

                    // The YOLO model's input name is "image" and output name is "grid"
                    var inputName = "image";
                    var outputName = "grid";

                    // Bind the image to the input
                    binding.Bind(inputName, image);

                    // Process the frame with the model
                    var results = await session.EvaluateAsync(binding, $"YoloRun {++runCount}");

                    Debug.WriteLine($"Evaluation Result Success? {results.Succeeded} ");

                    if (!results.Succeeded)
                    {
                        return;
                    }

                    Debug.WriteLine($" **** {results.Outputs.Count} Outputs Available ***** ");

                    foreach (var output in results.Outputs)
                    {
                        Debug.WriteLine($" - {output.Key}");
                    }

                    // Retrieve the results of evaluation
                    var resultTensor = results.Outputs[outputName] as TensorFloat;

                    Debug.WriteLine($"Result Feature Kind: {resultTensor?.Kind.ToString()} ");

                    var resultVector = resultTensor?.GetAsVectorView();

                    // Remove overlapping and low confidence bounding boxes
                    filteredBoxes = parser.NonMaxSuppress(parser.ParseOutputs(resultVector?.ToArray()), 5, .5F);
                    
                    Debug.WriteLine(filteredBoxes.Count <= 0 ? $"No Valid Bounding Boxes" : $"Valid Bounding Boxes: {filteredBoxes.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: EvaluateFrameException: {ex}");
            }
            finally
            {
                semaphore?.Release();

                Debug.WriteLine($"*************** Evaluating Video Frame [END] ***************");
            }
        }
        
        // Loads the ML model file and creates LearningModel
        private async Task LoadModelAsync()
        {
            try
            {
                Debug.WriteLine($"*************** LoadModelAsync [START] ***************");
                Debug.WriteLine($" - Locating ONYX file");

                var modelFile = await StorageFile.GetFileFromApplicationUriAsync(ModelUri);

                Debug.WriteLine($" - {modelFile.DisplayName} file located, attempting to create LearningModel");
                Debug.WriteLine($" - Attempting to create LearningModel...");

                model = await LearningModel.LoadFromStorageFileAsync(modelFile);

                Debug.WriteLine($" - {model.Name} LearningModel successfully instantiated.");

                modelCreated = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" - Error: Problem loading model file or creating LearningModel: {ex.Message}");
                model = null;
                modelCreated = false;
            }
            finally
            {
                Debug.WriteLine($"*************** LoadModelAsync [END] ***************");
            }
        }
        

        // ********** IBasicVideoEffect Requirements ********** //

        public async void SetProperties(IPropertySet configuration)
        {
            currentConfiguration = configuration;


            // Create the Learning model at the first opportunity
            await LoadModelAsync();
        }

        public void SetEncodingProperties(VideoEncodingProperties encodingProperties, IDirect3DDevice device)
        {
            currentEncodingProperties = encodingProperties;

            canvasDevice = device != null ? CanvasDevice.CreateFromDirect3D11Device(device) : CanvasDevice.GetSharedDevice();

            parser = new TinyYoloParser();
            filteredBoxes = new List<BoundingBox>();

            if (model == null)
            {
                // Use the appropriate option
                if (device == null)
                {
                    // startup WinML using CPU
                    detectedDeviceKind = LearningModelDeviceKind.Cpu;
                }
                else
                {
                    // Startup WinML using DirectX

                    // IF the frame rate is really high, we can probably expect a higher powered device
                    var frames = encodingProperties.FrameRate.Numerator;
                    var timeSpan = encodingProperties.FrameRate.Denominator;
                    var ratio = timeSpan / frames;

                    if (ratio > 0.04)
                    {
                        // Greater than 30 frames a second
                        detectedDeviceKind = LearningModelDeviceKind.DirectXHighPerformance;
                    }
                    else if (ratio > 0.01)
                    {
                        // If 30 frames a second or less, set expectations for WinML about what power is available
                        detectedDeviceKind = LearningModelDeviceKind.DirectX;
                    }
                    else
                    {
                        detectedDeviceKind = LearningModelDeviceKind.DirectXMinPower;
                    }
                }

                Debug.WriteLine($"Warning: LearningDeviceKind is set to {detectedDeviceKind}.");
                
                frameProcessingTimer = ThreadPoolTimer.CreatePeriodicTimer(EvaluateVideoFrame, poolTimerInterval);
            }   
        }

        public void Close(MediaEffectClosedReason reason)
        {
            canvasDevice?.Dispose();
            semaphore?.Dispose();
            frameProcessingTimer?.Cancel();
        }

        public MediaMemoryTypes SupportedMemoryTypes => EffectConstants.SupportedMemoryTypes;

        public IReadOnlyList<VideoEncodingProperties> SupportedEncodingProperties => EffectConstants.SupportedEncodingProperties;

        public bool IsReadOnly => false;
        public bool TimeIndependent => false;
        public void DiscardQueuedFrames() { }
    }
}