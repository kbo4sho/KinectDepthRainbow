//------------------------------------------------------------------------------
// <copyright file="MainPage.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using WindowsPreview.Kinect;

namespace Microsoft.Samples.Kinect.DepthBasics
{
    /// <summary>
    /// Main page for sample
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 1024;

        /// <summary>
        /// Resource loader for string resources
        /// </summary>
#if WIN81ORLATER
        private ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("Resources");
#else
        private ResourceLoader resourceLoader = new ResourceLoader("Resources");
#endif


        /// <summary>
        /// Size of the RGB pixel in the bitmap
        /// </summary>
        private readonly int cbytesPerPixel = 4;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for depth frames
        /// </summary>
        private DepthFrameReader depthFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap = null;

        /// <summary>
        /// Intermediate storage for receiving frame data from the sensor
        /// </summary>
        private ushort[] depthFrameData = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private byte[] depthPixels = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;


        private DispatcherTimer timer = null;

        /// <summary>
        /// Initializes a new instance of the MainPage class.
        /// </summary>
        public MainPage()
        {
            //timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(.01) };
            //timer.Tick += Timer_Tick;
            //timer.Start();
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // get the depthFrameDescription from the DepthFrameSource
            FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // open the reader for the depth frames
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;

            // allocate space to put the pixels being received and converted
            this.depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
            this.depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * this.cbytesPerPixel];

            // create the bitmap to display
            this.bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height);//, 96.0, 96.0, PixelFormats.Bgr32, null);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? resourceLoader.GetString("RunningStatusText")
                                                            : resourceLoader.GetString("NoSensorStatusText");

            // use the window object as the view model in this simple example
            this.DataContext = this;


            


            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        private void Timer_Tick(object sender, object e)
        {
            
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                // DepthFrameReder is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            ushort minDepth = 0;
            ushort maxDepth = 0;

            bool depthFrameProcessed = false;
            
            // DepthFrame is IDisposable
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                    // verify data and write the new depth frame data to the display bitmap
                    if (((depthFrameDescription.Width * depthFrameDescription.Height) == this.depthFrameData.Length) &&
                        (depthFrameDescription.Width == this.bitmap.PixelWidth) && (depthFrameDescription.Height == this.bitmap.PixelHeight))
                    {
                        // Copy the pixel data from the image to a temporary array
                        depthFrame.CopyFrameDataToArray(this.depthFrameData);

                        minDepth = depthFrame.DepthMinReliableDistance;

                        // Note: In order to see the full range of depth (including the less reliable far field depth)
                        // we are setting maxDepth to the extreme potential depth threshold
                        maxDepth = ushort.MaxValue;

                        // If you wish to filter by reliable depth distance, uncomment the following line:
                        //// maxDepth = depthFrame.DepthMaxReliableDistance
                        
                        depthFrameProcessed = true;
                    }
                }
            }

            // we got a frame, convert and render
            if (depthFrameProcessed)
            {
                Mod = Mod > 255 ? (byte)0 : (byte)(Mod + 1);

                ConvertDepthData(minDepth, maxDepth);

                RenderDepthPixels(this.depthPixels);
            }
        }

        private byte Mod;
        private byte Green;
        private byte Blue;



        /// <summary>
        /// Converts depth to RGB.
        /// </summary>
        /// <param name="frame"></param>
        private void ConvertDepthData(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;
            for (int i = 0; i < this.depthFrameData.Length; ++i)
            {
                // Get the depth for this pixel
                ushort depth = this.depthFrameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);

                if (intensity == 0)
                {
                    this.depthPixels[colorPixelIndex++] = 0;
                    this.depthPixels[colorPixelIndex++] = 0;
                    this.depthPixels[colorPixelIndex++] = 0;
                }
                else if (intensity <= Mod)
                {
                    var delta = Mod - intensity;
                       
                    if (delta <= 42)
                    {
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 0, 42, delta);
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 255;
                    }
                    else if (delta <= 85)
                    {
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 42, 85, delta));
                    }
                    else if (delta <= 126)
                    {
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 85, 126, delta);
                        this.depthPixels[colorPixelIndex++] = 0;
                    }
                    else if (delta <= 169)
                    {
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 126, 169, delta));
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = 0;
                    }
                    else if (delta <= 211)
                    {
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 169, 211, delta);
                    }
                    else if (delta <= 255)
                    {
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 211, 255, delta));
                        this.depthPixels[colorPixelIndex++] = 255;
                    }
                }
                else {
                    if (intensity > 211 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 211 + Mod, 255, intensity));
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 255;
                    }
                    else if (intensity > 169 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 169 + Mod, 211 + Mod, intensity);
                    }
                    else if (intensity > 126 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 126 + Mod, 169 + Mod, intensity));
                        this.depthPixels[colorPixelIndex++] = 0;

                    }
                    else if (intensity > 85 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 85 + Mod, 126 + Mod, intensity);
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = 0;
                    }
                    else if (intensity > 42 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 255;
                        this.depthPixels[colorPixelIndex++] = (byte)(-NormalizeToByte(0, 255, 42 + Mod, 85 + Mod, intensity));
                    }
                    else if (intensity >= 0 + Mod)
                    {
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = NormalizeToByte(0, 255, 0 + Mod, 42 + Mod, intensity);
                        this.depthPixels[colorPixelIndex++] = 255;
                    }

                    else
                    {
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 0;
                        this.depthPixels[colorPixelIndex++] = 255;
                    }
                }

                this.depthPixels[colorPixelIndex++] = 255;
            }
        }

        public byte NormalizeToByte(float from, float to, float min, float max, float toNormalize)
        {
            var value = (toNormalize - min) * (to - from) / (max - min);
            return (byte)value;
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        /// <param name="pixels">pixel data</param>
        private void RenderDepthPixels(byte[] pixels)
        {
            pixels.CopyTo(this.bitmap.PixelBuffer);
            this.bitmap.Invalidate();
            theImage.Source = this.bitmap;
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? resourceLoader.GetString("RunningStatusText")
                                                            : resourceLoader.GetString("SensorNotAvailableStatusText");
        }

        private void theImage_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Mod = 0;
        }
    }
}
