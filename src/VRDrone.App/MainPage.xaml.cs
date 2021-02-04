// <copyright file="MainPage.xaml.cs" company="Mark Lauter">
// Copyright (c) Mark Lauter. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

#define EMULATOR_OFF

#if EMULATOR_ON
using Messenger.Simulator;
using Tello.Simulator;
#else
using System.Net;
using Messenger.Udp;
#endif

using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Repository.Sqlite;
using Tello.App.MvvM;
using Tello.App.UWP.Services;
using Tello.App.ViewModels;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.ApplicationModel.Resources.Core;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.Globalization;
using Windows.UI.Core;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using VRDrone.App.Helpers;
using Windows.Media;
using Microsoft.AI.Skills.Vision.ObjectDetector;
using Microsoft.AI.Skills.SkillInterface;
using SkillHelper;
using FrameSourceHelper_UWP;

// The speech recognizer used throughout this sample.

namespace VRDrone.App
{
    public sealed partial class MainPage : Page
    {


        #region objdetect
        private IFrameSource m_frameSource = null;

        // Vision Skills
        // Skill wrappers
        private List<SkillWrapper> m_skillWrappers = new List<SkillWrapper>() { new SkillWrapper(new ObjectDetectorDescriptor()) };
        private IReadOnlyList<ISkillExecutionDevice> m_availableExecutionDevices = null;
        private ISkillFeatureImageDescriptor m_inputImageFeatureDescriptor = null;

        // Misc
        private BoundingBoxRenderer m_bboxRenderer = null;
        private List<bool> m_objectKindFilterList = null;

        // Frames
        private SoftwareBitmapSource m_processedBitmapSource;
        private VideoFrame m_renderTargetFrame = null;

        // Performance metrics
        private Stopwatch m_evalStopwatch = new Stopwatch();
        private float m_bindTime = 0;
        private float m_evalTime = 0;
        private Stopwatch m_renderStopwatch = new Stopwatch();

        // Locks
        private SemaphoreSlim m_lock = new SemaphoreSlim(1);
        #endregion
        #region speech
        private SpeechHelper speech;
        private SpeechRecognizer speechRecognizer;
        static public bool IsRecognizerEnable { set; get; } = false;
        private ResourceContext speechContext;
        private ResourceMap speechResourceMap;
        private Dictionary<string, string[]> VoiceCommands = null;
        bool isListening;
        bool IsConnected = false;
        private static uint HResultRecognizerNotFound = 0x8004503a;
        #endregion

        EngineContainer ApiContainer = new EngineContainer();
        public MainViewModel ViewModel { get; }
        private CoreDispatcher Coredispatcher;
        private readonly IUIDispatcher dispatcher;
        private readonly IUINotifier notifier;
#if EMULATOR_ON
        private DroneSimulator _simulator;
#endif
        #region speech
        /*
         //untuk WPF
        public static void ConvertUiElementToBitmap(UIElement elt, string path)
        {
            double h = elt.RenderSize.Height;
            double w = elt.RenderSize.Width;
            if (h > 0)
            {
                PresentationSource source = PresentationSource.FromVisual(elt);
                RenderTargetBitmap rtb = new RenderTargetBitmap((int)w, (int)h, 96, 96, PixelFormats.Default);

                VisualBrush sourceBrush = new VisualBrush(elt);
                DrawingVisual drawingVisual = new DrawingVisual();
                DrawingContext drawingContext = drawingVisual.RenderOpen();
                using (drawingContext)
                {
                    drawingContext.DrawRectangle(sourceBrush, null, new Rect(new Point(0, 0),
                          new Point(w, h)));
                }
                rtb.Render(drawingVisual);

                // return rtb;
                var encoder = new PngBitmapEncoder();
                var outputFrame = BitmapFrame.Create(rtb);
                encoder.Frames.Add(outputFrame);

                using (var file = System.IO.File.OpenWrite(path))
                {
                    encoder.Save(file);
                }
            }
        }
        */
        public async Task<StorageFile> CaptureElement(UIElement uiElement)
        {
            RenderTargetBitmap renderTarget = new RenderTargetBitmap();
            await renderTarget.RenderAsync(uiElement);

            IBuffer pixelBuffer = await renderTarget.GetPixelsAsync();
            SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixelBuffer, BitmapPixelFormat.Bgra8, (int)uiElement.RenderSize.Width, (int)uiElement.RenderSize.Height, BitmapAlphaMode.Premultiplied);
            Random rnd = new Random();
            string fName = DateTime.Now.ToString("dd_MM_yy_HH_mm_") + rnd.Next(1, 999) + ".jpg";
            var tempFile = await KnownFolders.PicturesLibrary.CreateFileAsync(fName);
            //var tempFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appdata:///temp/{fName}"));
            SaveSoftwareBitmapToFile(bitmap, tempFile);
            return tempFile;

        }
        public async Task<SoftwareBitmap> CaptureElementToBitmap(UIElement uiElement)
        {
            RenderTargetBitmap renderTarget = new RenderTargetBitmap();
            await renderTarget.RenderAsync(uiElement);

            IBuffer pixelBuffer = await renderTarget.GetPixelsAsync();
            SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixelBuffer, BitmapPixelFormat.Bgra8, (int)uiElement.RenderSize.Width, (int)uiElement.RenderSize.Height, BitmapAlphaMode.Premultiplied);
           
            return bitmap;

        }
        private async void SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile)
        {
            using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Create an encoder with the desired format
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                // Set the software bitmap
                encoder.SetSoftwareBitmap(softwareBitmap);

                // Set additional encoding parameters, if needed
                encoder.BitmapTransform.ScaledWidth = 960;
                encoder.BitmapTransform.ScaledHeight = 720;
                //encoder.BitmapTransform.Rotation = Windows.Graphics.Imaging.BitmapRotation.Clockwise90Degrees;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                encoder.IsThumbnailGenerated = true;

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception err)
                {
                    const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                    switch (err.HResult)
                    {
                        case WINCODEC_ERR_UNSUPPORTEDOPERATION:
                            // If the encoder does not support writing a thumbnail, then try again
                            // but disable thumbnail generation.
                            encoder.IsThumbnailGenerated = false;
                            break;
                        default:
                            throw;
                    }
                }

                if (encoder.IsThumbnailGenerated == false)
                {
                    await encoder.FlushAsync();
                }


            }
        }
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    //StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    //StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            /*
            StatusBlock2.Text = strMessage;

            // Collapse the StatusBlock2 if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock2.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock2.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }*/
        }
        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };
        async void PlaySound(string SoundFile)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {

                MediaElement mysong = new MediaElement();
                Windows.Storage.StorageFolder folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
                Windows.Storage.StorageFile file = await folder.GetFileAsync(SoundFile);
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                mysong.SetSource(stream, file.ContentType);
                mysong.Play();
                //UI code here
            });
        }
        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);

                await speech.Read("VR Drone is ready");
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }

        void PopulateCommands()
        {
           
            VoiceCommands = new Dictionary<string, string[]>();
            VoiceCommands.Add(TagCommands.TakeOff, new string[] { "Take Off" });
       
            VoiceCommands.Add(TagCommands.Landing, new string[] { "Landing" });
            VoiceCommands.Add(TagCommands.SeeMe, new string[] { "What do you see" });
            VoiceCommands.Add(TagCommands.Forward, new string[] { "Move Forward" });
            VoiceCommands.Add(TagCommands.Backward, new string[] { "Move Backward" });
            VoiceCommands.Add(TagCommands.TurnLeft, new string[] { "Turn Left" });
            VoiceCommands.Add(TagCommands.TurnRight, new string[] { "Turn Right" });
            VoiceCommands.Add(TagCommands.MoveLeft, new string[] { "Move Left" });

            VoiceCommands.Add(TagCommands.MoveRight, new string[] { "Move Right" });
            VoiceCommands.Add(TagCommands.TakePhoto, new string[] { "Take a Picture" });

            VoiceCommands.Add(TagCommands.Thanks, new string[] { "Thanks","Thank You" });
            
            VoiceCommands.Add(TagCommands.Connect, new string[] { "Activate Drone" });
            VoiceCommands.Add(TagCommands.Disconnect, new string[] { "Shutdown Drone" });

            VoiceCommands.Add(TagCommands.ActivateVideo, new string[] { "Turn On Video" });
            VoiceCommands.Add(TagCommands.DeactivateVideo, new string[] { "Turn Off Video" });

            VoiceCommands.Add(TagCommands.MoveUp, new string[] { "Go Up" });
            VoiceCommands.Add(TagCommands.MoveDown, new string[] { "Go Down" });

        }

        /// <summary>
        /// Provide feedback to the user based on whether the recognizer is receiving their voice input.
        /// </summary>
        /// <param name="sender">The recognizer that is currently running.</param>
        /// <param name="args">The current state of the recognizer.</param>
        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            //await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    this.NotifyUser(args.State.ToString(), NotifyType.StatusMessage);
            //});
        }


        async void TurnRecognizer1()
        {
            if (isListening == false)
            {
                // The recognizer can only start listening in a continuous fashion if the recognizer is currently idle.
                // This prevents an exception from occurring.
                if (speechRecognizer.State == SpeechRecognizerState.Idle)
                {
                    try
                    {
                        await speechRecognizer.ContinuousRecognitionSession.StartAsync();

                       
                        isListening = true;
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
            else
            {
                isListening = false;

              
                if (speechRecognizer.State != SpeechRecognizerState.Idle)
                {
                    try
                    {
                        // Cancelling recognition prevents any currently recognized speech from
                        // generating a ResultGenerated event. StopAsync() will allow the final session to 
                        // complete.
                        await speechRecognizer.ContinuousRecognitionSession.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                        await messageDialog.ShowAsync();
                    }
                }
            }
        }
        /// <summary>
        /// Handle events fired when error conditions occur, such as the microphone becoming unavailable, or if
        /// some transient issues occur.
        /// </summary>
        /// <param name="sender">The continuous recognition session</param>
        /// <param name="args">The state of the recognizer</param>
        private async void ContinuousRecognitionSession_Completed(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
        {
            if (args.Status != SpeechRecognitionResultStatus.Success)
            {
                await Coredispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    this.NotifyUser("Continuous Recognition Completed: " + args.Status.ToString(), NotifyType.StatusMessage);

                   //cbLanguageSelection.IsEnabled = true;
                    isListening = false;
                });
            }
        }
        /// <summary>
        /// Initialize Speech Recognizer and compile constraints.
        /// </summary>
        /// <param name="recognizerLanguage">Language to use for the speech recognizer</param>
        /// <returns>Awaitable task.</returns>
        private async Task InitializeRecognizer(Language recognizerLanguage)
        {
            if (speechRecognizer != null)
            {
                // cleanup prior to re-initializing this scenario.
                speechRecognizer.StateChanged -= SpeechRecognizer_StateChanged;
                speechRecognizer.ContinuousRecognitionSession.Completed -= ContinuousRecognitionSession_Completed;
                speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= ContinuousRecognitionSession_ResultGenerated;

                this.speechRecognizer.Dispose();
                this.speechRecognizer = null;
            }

            try
            {
                this.speechRecognizer = new SpeechRecognizer(recognizerLanguage);

                // Provide feedback to the user about the state of the recognizer. This can be used to provide visual feedback in the form
                // of an audio indicator to help the user understand whether they're being heard.
                speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;

                // Build a command-list grammar. Commands should ideally be drawn from a resource file for localization, and 
                // be grouped into tags for alternate forms of the same command.
                foreach (var item in VoiceCommands)
                {
                    speechRecognizer.Constraints.Add(
                   new SpeechRecognitionListConstraint(
                      item.Value, item.Key));
                }


              
                SpeechRecognitionCompilationResult result = await speechRecognizer.CompileConstraintsAsync();
                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    // Disable the recognition buttons.
                    //btnContinuousRecognize.IsEnabled = false;
                    IsRecognizerEnable = false;
                    // Let the user know that the grammar didn't compile properly.
                    //resultTextBlock.Visibility = Visibility.Visible;
                    //resultTextBlock.Text = "Unable to compile grammar.";
                }
                else
                {
                    //btnContinuousRecognize.IsEnabled = true;

                    //resultTextBlock.Visibility = Visibility.Collapsed;
                    IsRecognizerEnable = true;

                    // Handle continuous recognition events. Completed fires when various error states occur. ResultGenerated fires when
                    // some recognized phrases occur, or the garbage rule is hit.
                    speechRecognizer.ContinuousRecognitionSession.Completed += ContinuousRecognitionSession_Completed;
                    speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                }
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == HResultRecognizerNotFound)
                {
                    //btnContinuousRecognize.IsEnabled = false;
                    IsRecognizerEnable = false;
                    //resultTextBlock.Visibility = Visibility.Visible;
                    //resultTextBlock.Text = "Speech Language pack for selected language not installed.";
                }
                else
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog(ex.Message, "Exception");
                    await messageDialog.ShowAsync();
                }
            }

        }
        
        private async Task<StorageFile> TakePhoto()
        {
            try
            {
                return await CaptureElement(VideoElement);
            }
            catch (Exception)
            {

                return null;
            }
          
          
        }
        /// <summary>
        /// Handle events fired when a result is generated. This may include a garbage rule that fires when general room noise
        /// or side-talk is captured (this will have a confidence of Rejected typically, but may occasionally match a rule with
        /// low confidence).
        /// </summary>
        /// <param name="sender">The Recognition session that generated this result</param>
        /// <param name="args">Details about the recognized speech</param>
        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // The garbage rule will not have a tag associated with it, the other rules will return a string matching the tag provided
            // when generating the grammar.
            string tag = "unknown";
            if (args.Result.Constraint != null)
            {
                tag = args.Result.Constraint.Tag;
            }

            // Developers may decide to use per-phrase confidence levels in order to tune the behavior of their 
            // grammar based on testing.
            if (args.Result.Confidence == SpeechRecognitionConfidence.Low ||
                args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                await Coredispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                  
                    //heardYouSayTextBlock.Visibility = Visibility.Visible;
                    //resultTextBlock.Visibility = Visibility.Visible;
                    //resultTextBlock.Text = string.Format("Heard: '{0}', (Tag: '{1}', Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                    var controller = this.ViewModel.ControllerViewModel;
                    switch (tag)
                    {

                        case TagCommands.Connect:
                            {
                                controller.EnterSdkModeCommand.Execute(null);
                                await speech.Read("drone is connected");
                                IsConnected = true;
                            }
                            break;
                        case TagCommands.Disconnect:
                            {
                                controller.DisconnectCommand.Execute(null);
                                await speech.Read("drone is disconnected");
                                IsConnected = false;
                            }
                            break;
                        case TagCommands.TakeOff:
                            {
                                controller.TakeOffCommand.Execute(null);
                                await speech.Read("drone is take off");
                            }
                            break;
                        case TagCommands.Landing:
                            {
                                controller.LandCommand.Execute(null);
                                await speech.Read("drone is landing");
                            }
                            break;
                        case TagCommands.ActivateVideo:
                            {
                                controller.StartVideoCommand.Execute(null);
                                await speech.Read("video is active");
                            }
                            break;
                        case TagCommands.DeactivateVideo:
                            {
                                controller.StopVideoCommand.Execute(null);
                                await speech.Read("video is off");
                            }
                            break;
                        case TagCommands.Forward:
                            {
                                controller.GoForwardCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.Backward:
                            {
                                controller.GoBackwardCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.TurnLeft:
                            {
                                controller.TurnLeftCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.TurnRight:
                            {
                                controller.TurnRightCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.MoveLeft:
                            {
                                controller.GoLeftCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.MoveRight:
                            {
                                controller.GoRightCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.MoveUp:
                            {
                                controller.GoUpCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.MoveDown:
                            {
                                controller.GoDownCommand.Execute(null);
                                await speech.Read("ok");
                            }
                            break;
                        case TagCommands.SeeMe:
                            {
                                
                                var photo = await CaptureElementToBitmap(VideoElement);
                                //call computer vision
                                if (photo != null)
                                {
                                    if (m_lock.Wait(0))
                                    {
                                        #pragma warning disable CS4014
                                        // Purposely don't await this: want handler to exit ASAP
                                        // so that realtime capture doesn't wait for completion.
                                        // Instead, we unlock only when processing finishes ensuring that
                                        // only one execution is active at a time, dropping frames or
                                        // aborting skill runs as necessary
                                        Task.Run(async () =>
                                        {
                                            try
                                            {
                                                var frame = VideoFrame.CreateWithSoftwareBitmap(photo);
                                                // Retrieve and filter results if requested
                                                IReadOnlyList<ObjectDetectorResult> detectedObjects = await DetectObjectsAsync(frame);
                                                await DisplayFrameAndResultAsync(frame, detectedObjects);
                                            }
                                            catch (Exception ex)
                                            {
                                                NotifyUser(ex.Message);
                                            }
                                            finally
                                            {
                                                m_lock.Release();
                                            }
                                        });
                                        #pragma warning restore CS4014
                                    }
                                    /*
                                    var res = await ApiContainer.GetApi<ComputerVisionService>().RecognizeImage(photo);
                                    if (!string.IsNullOrEmpty(res))
                                    {
                                        await speech.Read(res);
                                        //resultTextBlock.Text = "I see " + res;
                                    }*/
                                }
                                else
                                {
                                    await speech.Read("cannot get video frame");
                                }
                            }
                            break;
                       

                        case TagCommands.TakePhoto:
                            var img = await TakePhoto();
                            await speech.Read("photo captured");
                            //GetPhotoFromCam();
                            break;
                        case TagCommands.Thanks:
                            await speech.Read("My pleasure boss");
                            break;
                      
                    }
                });
            }
            else
            {
                // In some scenarios, a developer may choose to ignore giving the user feedback in this case, if speech
                // is not the primary input mechanism for the application.
                await Coredispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //heardYouSayTextBlock.Visibility = Visibility.Collapsed;
                    //resultTextBlock.Visibility = Visibility.Visible;
                    //resultTextBlock.Text = string.Format("Sorry, I didn't catch that. (Heard: '{0}', Tag: {1}, Confidence: {2})", args.Result.Text, tag, args.Result.Confidence.ToString());
                });
            }
        }
        #endregion
        private MainViewModel CreateMainViewModel(IUIDispatcher dispatcher, IUINotifier notifier)
        {
#if EMULATOR_ON
            _simulator = new DroneSimulator();
            var transceiver = new SimTransceiver(_simulator.MessageHandler);
            var stateReceiver = new SimReceiver(_simulator.StateTransmitter);
            var videoReceiver = new SimReceiver(_simulator.VideoTransmitter);
#else
            var transceiver = new UdpTransceiver(IPAddress.Parse("192.168.10.1"), 8889);
            var stateReceiver = new UdpReceiver(8890);
            var videoReceiver = new UdpReceiver(11111);
#endif
            return new MainViewModel(
                this.dispatcher,
                this.notifier,
                new SqliteRepository((null, "tello.sqlite")),
                transceiver,
                stateReceiver,
                videoReceiver);
        }

        public MainPage()
        {
            this.InitializeComponent();
            // Initialize helper class used to render the skill results on screen
            m_bboxRenderer = new BoundingBoxRenderer(UIOverlayCanvas);
            m_processedBitmapSource = new SoftwareBitmapSource();

            PopulateCommands();
            ApiContainer.Register<ComputerVisionService>(new ComputerVisionService());
            this.dispatcher = new UIDispatcher(SynchronizationContext.Current);
            this.notifier = new UINotifier();

            this.ViewModel = this.CreateMainViewModel(this.dispatcher, this.notifier);
            this.DataContext = this.ViewModel;
            this.ViewModel.ControllerViewModel.PropertyChanged += this.ControllerViewModel_PropertyChanged;
           
            isListening = false;
        }

        private void ControllerViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var property = typeof(TelloControllerViewModel).GetProperty(e.PropertyName);
            var value = property.GetValue(sender);
            Debug.WriteLine($"{nameof(this.ControllerViewModel_PropertyChanged)} - property '{e.PropertyName}', value '{value}'");

            if (e.PropertyName == nameof(TelloControllerViewModel.IsVideoStreaming) && (bool)value)
            {
                this.VideoElement.Play();
            }
            else
            {
                this.VideoElement.Stop();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            this.ViewModel.Open();

            this.InitializeVideo();

            //speech

            // Keep track of the UI thread dispatcher, as speech events will come in on a separate thread.
            Coredispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            // Prompt the user for permission to access the microphone. This request will only happen
            // once, it will not re-prompt if the user rejects the permission.
            bool permissionGained = await AudioCapturePermissions.RequestMicrophonePermission();
            if (permissionGained)
            {

                // Initialize resource map to retrieve localized speech strings.
                Language speechLanguage = SpeechRecognizer.SystemSpeechLanguage;
                string langTag = speechLanguage.LanguageTag;
                speechContext = ResourceContext.GetForCurrentView();
                speechContext.Languages = new string[] { langTag };

                speechResourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("LocalizationSpeechResources");


                await InitializeRecognizer(SpeechRecognizer.SystemSpeechLanguage);

                TurnRecognizer1();
            }
            else
            {
                //this.resultTextBlock.Visibility = Visibility.Visible;
                //this.resultTextBlock.Text = "Permission to access capture resources was not given by the user, reset the application setting in Settings->Privacy->Microphone.";
                //cbLanguageSelection.IsEnabled = false;

            }

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.ViewModel.Close();

            base.OnNavigatedFrom(e);
        }

        #region video display

        private bool videoInitilized = false;

        private void InitializeVideo()
        {
            if (!this.videoInitilized)
            {
                this.videoInitilized = true;

                var videoEncodingProperties = VideoEncodingProperties.CreateH264();
                videoEncodingProperties.Height = 720;
                videoEncodingProperties.Width = 960;

                var mediaStreamSource = new MediaStreamSource(new VideoStreamDescriptor(videoEncodingProperties))
                {
                    // never turn live on because it tries to skip frame which breaks the h264 decoding
                    // IsLive = true,
                    BufferTime = TimeSpan.FromSeconds(0.0),
                };
            
                mediaStreamSource.SampleRequested += this.MediaStreamSource_SampleRequested;

                this.VideoElement.SetMediaStreamSource(mediaStreamSource);
                
                // never turn real time playback on
                // _mediaElement.RealTimePlayback = true;
            }
        }

      

        private void MediaStreamSource_SampleRequested(
            MediaStreamSource sender,
            MediaStreamSourceSampleRequestedEventArgs args)
        {
#if EMULATOR_ON
#else
            var sample = this.ViewModel.VideoViewModel.GetSample();
            
            // Debug.WriteLine($"{nameof(MediaStreamSource_SampleRequested)} - video ready? {sample != null}");
            if (sample != null)
            {
                // Debug.WriteLine($"{nameof(MediaStreamSource_SampleRequested)} - got sample time index {sample.TimeIndex}, length {sample.Buffer.Length}b, duration {sample.Duration}");
                args.Request.Sample = MediaStreamSample.CreateFromBuffer(sample.Buffer.AsBuffer(), sample.TimeIndex);
                args.Request.Sample.Duration = sample.Duration;
               
            }
#endif
        }
        #endregion


       
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
           
            m_lock.Wait();
            {
                //NotifyUser("Initializing skill...");
                m_availableExecutionDevices = m_skillWrappers[0].ExecutionDevices;

                await InitializeObjectDetectorAsync();
                //await UpdateSkillUIAsync();
            }
            m_lock.Release();
        }
        /// <summary>
        /// Initialize the ObjectDetector skill
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        private async Task InitializeObjectDetectorAsync(ISkillExecutionDevice device = null)
        {
            await m_skillWrappers[0].InitializeSkillAsync(device);
            m_inputImageFeatureDescriptor = m_skillWrappers[0].Binding["InputImage"].Descriptor as SkillFeatureImageDescriptor;
        }

        /// <summary>
        /// Bind and evaluate the frame with the ObjectDetector skill
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        private async Task<IReadOnlyList<ObjectDetectorResult>> DetectObjectsAsync(VideoFrame frame)
        {
            m_evalStopwatch.Restart();

            var objDetectionBinding = m_skillWrappers[0].Binding as ObjectDetectorBinding;

            // Bind
            await objDetectionBinding.SetInputImageAsync(frame);

            m_bindTime = (float)m_evalStopwatch.ElapsedTicks / Stopwatch.Frequency * 1000f;
            m_evalStopwatch.Restart();

            // Evaluate
            await m_skillWrappers[0].Skill.EvaluateAsync(objDetectionBinding);

            m_evalTime = (float)m_evalStopwatch.ElapsedTicks / Stopwatch.Frequency * 1000f;
            m_evalStopwatch.Stop();

            return objDetectionBinding.DetectedObjects;
        }

        /// <summary>
        /// Render ObjectDetector skill results
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="objectDetections"></param>
        /// <returns></returns>
        private async Task DisplayFrameAndResultAsync(VideoFrame frame, IReadOnlyList<ObjectDetectorResult> detectedObjects)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    SoftwareBitmap targetSoftwareBitmap = frame.SoftwareBitmap;

                    // If we receive a Direct3DSurface-backed VideoFrame, convert to a SoftwareBitmap in a format that can be rendered via the UI element
                    if (targetSoftwareBitmap == null)
                    {
                        if (m_renderTargetFrame == null)
                        {
                            m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, frame.Direct3DSurface.Description.Width, frame.Direct3DSurface.Description.Height, BitmapAlphaMode.Ignore);
                        }

                        // Leverage the VideoFrame.CopyToAsync() method that can convert the input Direct3DSurface-backed VideoFrame to a SoftwareBitmap-backed VideoFrame
                        await frame.CopyToAsync(m_renderTargetFrame);
                        targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                    }
                    // Else, if we receive a SoftwareBitmap-backed VideoFrame, if its format cannot already be rendered via the UI element, convert it accordingly
                    else
                    {
                        if (targetSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || targetSoftwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Ignore)
                        {
                            if (m_renderTargetFrame == null)
                            {
                                m_renderTargetFrame = new VideoFrame(BitmapPixelFormat.Bgra8, targetSoftwareBitmap.PixelWidth, targetSoftwareBitmap.PixelHeight, BitmapAlphaMode.Ignore);
                            }

                            // Leverage the VideoFrame.CopyToAsync() method that can convert the input SoftwareBitmap-backed VideoFrame to a different format
                            await frame.CopyToAsync(m_renderTargetFrame);
                            targetSoftwareBitmap = m_renderTargetFrame.SoftwareBitmap;
                        }
                    }
                    await m_processedBitmapSource.SetBitmapAsync(targetSoftwareBitmap);

                    // Update displayed results
                    m_bboxRenderer.Render(detectedObjects);

                    // Update the displayed performance text
                    //UIPerfTextBlock.Text = $"bind: {m_bindTime.ToString("F2")}ms, eval: {m_evalTime.ToString("F2")}ms";
                }
                catch (TaskCanceledException)
                {
                    // no-op: we expect this exception when we change media sources
                    // and can safely ignore/continue
                }
                catch (Exception ex)
                {
                    NotifyUser($"Exception while rendering results: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Configure an IFrameSource from a StorageFile or MediaCapture instance to produce optionally a specified format of frame
        /// </summary>
        /// <param name="source"></param>
        /// <param name="inputImageDescriptor"></param>
        /// <returns></returns>
        private async Task ConfigureFrameSourceAsync(object source, ISkillFeatureImageDescriptor inputImageDescriptor = null)
        {
            await m_lock.WaitAsync();
            {
                // Reset bitmap rendering component
                VideoElement.Source = null;
                m_renderTargetFrame = null;
                //VideoElement.Source = m_processedBitmapSource;

                // Clean up previous frame source
                if (m_frameSource != null)
                {
                    //m_frameSource.FrameArrived -= FrameSource_FrameAvailable;
                    var disposableFrameSource = m_frameSource as IDisposable;
                    if (disposableFrameSource != null)
                    {
                        // Lock disposal based on frame source consumers
                        disposableFrameSource.Dispose();
                    }
                }

                // Create new frame source and register a callback if the source fails along the way
                m_frameSource = await FrameSourceFactory.CreateFrameSourceAsync(
                    source,
                    (sender, message) =>
                    {
                        NotifyUser(message);
                    },
                    inputImageDescriptor);

                // TODO: Workaround for a bug in ObjectDetectorBinding when binding consecutively VideoFrames with Direct3DSurface and SoftwareBitmap
                await m_skillWrappers[0].InitializeSkillAsync(m_skillWrappers[0].Skill.Device);

                // Set additional input features as exposed in the UI
                await m_skillWrappers[0].Binding["InputObjectKindFilterList"].SetFeatureValueAsync(m_objectKindFilterList);
                //await m_skillWrappers[0].Binding["InputConfidenceThreshold"].SetFeatureValueAsync((float)UIConfidenceThresholdControl.Value);
            }
            m_lock.Release();

            // If we obtained a valid frame source, start it
            if (m_frameSource != null)
            {
                //m_frameSource.FrameArrived += FrameSource_FrameAvailable;
                await m_frameSource.StartAsync();
            }
        }
        
        /// <summary>
        /// Print a message to the UI
        /// </summary>
        /// <param name="message"></param>
        private void NotifyUser(String message)
        {
            if (Dispatcher.HasThreadAccess)
            {
                //UIMessageTextBlock.Text = message;
            }
            else
            {
                //Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => UIMessageTextBlock.Text = message).AsTask().Wait();
            }
        }
        /// <summary>
        /// Triggered when the image control is resized, making sure the canvas size stays in sync with the frame display control.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoElement_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Make sure the aspect ratio is honored when rendering the body limbs
            float cameraAspectRatio = 1.0f;// (float)m_frameSource.FrameWidth / m_frameSource.FrameHeight;
            float previewAspectRatio = (float)(VideoElement.ActualWidth / VideoElement.ActualHeight);
            UIOverlayCanvas.Width = cameraAspectRatio >= previewAspectRatio ? VideoElement.ActualWidth : VideoElement.ActualHeight * cameraAspectRatio;
            UIOverlayCanvas.Height = cameraAspectRatio >= previewAspectRatio ? VideoElement.ActualWidth / cameraAspectRatio : VideoElement.ActualHeight;

            m_bboxRenderer.ResizeContent(e);
        }


    }
}
