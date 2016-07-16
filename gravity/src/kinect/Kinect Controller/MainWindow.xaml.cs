//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Ports;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using NAudio;
    using NAudio.Wave;
    using System.Runtime.InteropServices;
    using System.Text;
    using Image = System.Drawing.Image;
    using pixel = System.Drawing.Color;
    using pixelTranslator = System.Drawing.ColorTranslator;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    /// 

    public abstract class WaveProvider32 : IWaveProvider
    {
        private WaveFormat waveFormat;
        private WaveOut waveOut;
        public WaveProvider32()
            : this(44100, 1)
        {
        }

        public WaveProvider32(int sampleRate, int channels)
        {
            SetWaveFormat(sampleRate, channels);
        }

        public void SetWaveFormat(int sampleRate, int channels)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            WaveBuffer waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
        }

        public abstract int Read(float[] buffer, int offset, int sampleCount);

        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }
    }

    public class SineWaveProvider32 : WaveProvider32
    {
        int sample;

        public SineWaveProvider32()
        {
            Frequency = 1000;
            Amplitude = 0.25f; // let's not hurt our ears            
        }

        public float Frequency { get; set; }
        public float Amplitude { get; set; }

        // The sample that the stop command was called at.
        private int stopSample = 0;

        public int Sample { get; set; }
        public bool IsPlaying { get; set; }

        public void InitiateStop()
        {
            IsPlaying = false;
            stopSample = Sample;
        }

        public override int Read(float[] buffer, int offset, int sampleCount)
        {
            int sampleRate = WaveFormat.SampleRate;
            if (IsPlaying)
            {
                for (int n = 0; n < sampleCount; n++)
                {
                    buffer[n + offset] = (float)(Amplitude * Math.Sin((2 * Math.PI * sample * Frequency) / sampleRate));
                    sample++;
                    if (sample >= sampleRate) sample = 0;
                }
            }
            else
            {
                for (int n = 0; n < sampleCount; n++)
                {
                    buffer[n + offset] = (float)(Amplitude * Math.Sin((2 * Math.PI * sample * Frequency) / sampleRate));
                    sample++;
                    if (sample >= sampleRate) sample = 0;
                }
            }
            return sampleCount;
        }
    }
    public class Teensy
    {
        public int Panel_Number { get; set; }

        public int Index { get; set; }

        public string COM_Port { get; set; }
        public SerialPort _serialPort { get; set; }
            
    }

    public class Panel
    {
        public int Panel_Number { get; set; }
        public List<Teensy> Teensys_in_Panel { get; set; }
        public pixel[,] Pixels { get; set; } //2 demintional array of pixels


    }

    public class tunnel
    {
        public Panel Left { get; set; }
        public Panel Right { get; set; }
        public Panel Up_Right { get; set; }
        public Panel Up_Left { get; set; }
        public Panel Top { get; set; }


    }


    class INIFile
    {
        private string filePath;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section,
        string key,
        string val,
        string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section,
        string key,
        string def,
        StringBuilder retVal,
        int size,
        string filePath);

        public INIFile(string filePath)
        {
            this.filePath = filePath;
        }

        public void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value.ToLower(), this.filePath);
        }

        public string Read(string section, string key)
        {
            StringBuilder SB = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, "", SB, 255, this.filePath);
            return SB.ToString();
        }

        public string FilePath
        {
            get { return this.filePath; }
            set { this.filePath = value; }
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        tunnel Tunnel = new tunnel();
        
        //static SerialPort _serialPort;
        IWavePlayer waveOutDevice;
        AudioFileReader audioFileReader;

        private WaveOut waveOut;

        int Body_Leader_Index = -1;

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\logs\WriteLines2.csv");

        private static void DataReceivedHandler(
                              object sender,
                              SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Console.WriteLine("Data Received:");
            Console.Write(indata);
        }

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            using (var tcp = new TcpClient("127.0.0.1", 1337))
            {
                string image = "";

                for(int i = 0; i < 13200; i++)
                {
                    image += "FF0000";
                }

                tcp.GetStream().Write(Encoding.ASCII.GetBytes(image), 0, image.Length);
            }

            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

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
        bool alternate = true;

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
            waveOutDevice = new WaveOut();
            string Teensy_Num, com_port, panel_setup;
            int panel, index;

            //read INIfile to get configuration of Teensy's
            INIFile inif = new INIFile("C:\\Users\\vitaly\\Dropbox (Personal)\\Kinect Controller\\Teensy_Setup.ini");

            Tunnel.Left = new Panel();
            Tunnel.Left.Teensys_in_Panel = new List<Teensy>();
            Tunnel.Left.Pixels = new pixel[150, 8];
            Tunnel.Right = new Panel();
            Tunnel.Right.Teensys_in_Panel = new List<Teensy>();
            Tunnel.Right.Pixels = new pixel[150, 8];
            Tunnel.Up_Left = new Panel();
            Tunnel.Up_Left.Teensys_in_Panel = new List<Teensy>();
            Tunnel.Up_Left.Pixels = new pixel[150, 8];
            Tunnel.Up_Right = new Panel();
            Tunnel.Up_Right.Teensys_in_Panel = new List<Teensy>();
            Tunnel.Up_Right.Pixels = new pixel[150, 8];
            Tunnel.Top = new Panel();
            Tunnel.Top.Teensys_in_Panel = new List<Teensy>();
            Tunnel.Top.Pixels = new pixel[150, 8];
            


            for (int T = 1; T <= 11; T++)
            {
                Teensy_Num = "T" + T.ToString();
                com_port = inif.Read("COM Setup", Teensy_Num);
                panel_setup = inif.Read("Panel Setup", Teensy_Num);
                panel = Int32.Parse(panel_setup.Substring(5, 1));
                index = Int32.Parse(panel_setup.Substring(7, 1));
                SerialPort sp = new SerialPort("COM25", 9600, Parity.None, 8, StopBits.One);
                sp.ReadTimeout = 500;
                sp.WriteTimeout = 500;
                sp.WriteBufferSize = 2000;

                try
                {
                    sp.Open();  //try to open each teensy from ini file
                }
                catch { }
                //  = new List<SerialPort>();
                if (sp.IsOpen)
                {
                    
                    switch (panel)
                    {
                        case (0):
                            Tunnel.Left.Teensys_in_Panel.Add(new Teensy() { Panel_Number = panel, Index = index, COM_Port = com_port, _serialPort = sp });
                            break;
                        case (1):
                            Tunnel.Up_Left.Teensys_in_Panel.Add(new Teensy() { Panel_Number = panel, Index = index, COM_Port = com_port, _serialPort = sp });
                            break;
                        case (2):
                            Tunnel.Top.Teensys_in_Panel.Add(new Teensy() { Panel_Number = panel, Index = index, COM_Port = com_port, _serialPort = sp });
                            break;
                        case (3):
                            Tunnel.Up_Right.Teensys_in_Panel.Add(new Teensy() { Panel_Number = panel, Index = index, COM_Port = com_port, _serialPort = sp });
                            break;
                        case (4):
                            Tunnel.Right.Teensys_in_Panel.Add(new Teensy() { Panel_Number = panel, Index = index, COM_Port = com_port, _serialPort = sp });
                            break;
                    }
                }
                System.Threading.Thread.Sleep(10);
            }
          


        //    update GUI List of opened ports to teensy's for each panel
            TEENSYS_LEFT.ItemsSource = Tunnel.Left.Teensys_in_Panel;
            TEENSYS_UPLEFT.ItemsSource = Tunnel.Up_Left.Teensys_in_Panel;
            TEENSYS_TOP.ItemsSource = Tunnel.Top.Teensys_in_Panel;
            TEENSYS_UPRIGHT.ItemsSource = Tunnel.Up_Right.Teensys_in_Panel;
            TEENSYS_RIGHT.ItemsSource = Tunnel.Right.Teensys_in_Panel;
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
            file.Close();
            //_serialPorts[0].Close();
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size
                    dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

                    int penIndex = 0;
                    
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked)
                        {
                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }
                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            this.DrawBody(joints, jointPoints, dc, drawPen);

                            this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                            this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);

                            if((TrackingState.Tracked == joints[JointType.KneeRight].TrackingState) && (TrackingState.Tracked == joints[JointType.WristRight].TrackingState) && (TrackingState.Tracked == joints[JointType.Head].TrackingState))
                            {
                                double RightHandRaisedRatio = (jointPoints[JointType.WristRight].Y- jointPoints[JointType.KneeRight].Y*.85) / (jointPoints[JointType.Head].Y - jointPoints[JointType.KneeRight].Y*.85);
                                double LeftHandRaisedRatio = (jointPoints[JointType.WristLeft].Y - jointPoints[JointType.KneeLeft].Y * .85) / (jointPoints[JointType.Head].Y - jointPoints[JointType.KneeLeft].Y * .85);
                                string Right_Ratio = RightHandRaisedRatio.ToString("0.000");
                                string Left_Ratio = LeftHandRaisedRatio.ToString("0.000");
                                string SerialData = "H" + Right_Ratio + "," + Left_Ratio;
                                
                                if ((RightHandRaisedRatio>1)&&(LeftHandRaisedRatio>1)&& (body.HandLeftState == HandState.Closed)&& (body.HandRightState == HandState.Closed)&&(waveOutDevice.PlaybackState==PlaybackState.Stopped))
                                {

                                    Body_Leader_Index = penIndex - 1; //set body leader index to current body if both hands raised
                                    audioFileReader = new AudioFileReader("../../../Audio Files/Player_Identified.mp3");
                                    waveOutDevice.Init(audioFileReader);
                                    waveOutDevice.Play();


                                }


                                if ((RightHandRaisedRatio > 0) && (RightHandRaisedRatio < 2) && (LeftHandRaisedRatio > 0) && (LeftHandRaisedRatio < 2)&& (Body_Leader_Index == penIndex - 1))
                                {
                                    /*
                                    //Create a tone proportional to how high the hand is raised. 
                                   
                                    if (waveOut == null)
                                    {
                                        var sineWaveProvider = new SineWaveProvider32();
                                        if ((waveOutDevice.PlaybackState == PlaybackState.Stopped))
                                        {
                                            sineWaveProvider.SetWaveFormat(16000, 1); // 16kHz mono
                                            sineWaveProvider.Frequency = (float)RightHandRaisedRatio * 800;
                                            sineWaveProvider.Amplitude = 0.25f;
                                            waveOut = new WaveOut();
                                            waveOut.Init(sineWaveProvider);
                                            waveOut.Play();
                                        }
                                    }
                                    else
                                    {
                                        waveOut.Stop();
                                        waveOut.Dispose();
                                        waveOut = null;
                                    }
                                    */

                                    //We know right hand is raised, now we need to translate that to our function to map it on the tunnel
                                    //Use Panel 1 and 5 for (0-100%)
                                    //update the tunnel object's pixels

                                    for (int x = 0; x < 150; x++)
                                    {
                                        //update left and right to depend on left and right hand ratios
                                        for (int y = 0; y < 8; y++)
                                        {
                                            if ((y / 32) < LeftHandRaisedRatio)
                                            {
                                                Tunnel.Left.Pixels[x, y] = pixel.FromArgb(255,0,0);
                                            }
                                            else
                                            {
                                                Tunnel.Left.Pixels[x, y] = pixel.FromArgb(0, 255, 0);
                                            }

                                            if ((y / 32) < RightHandRaisedRatio)
                                            {
                                                Tunnel.Right.Pixels[x, y] = pixel.FromArgb(0, 0, 255);
                                            }
                                            else
                                            {
                                                Tunnel.Right.Pixels[x, y] = pixel.FromArgb(0, 255, 0);
                                            }
                                        }
                                        //update top to alternate between blue and red for every refresh

                                        for (int y = 0; y < 8; y++)
                                        {
                                            if (alternate)
                                            {
                                                Tunnel.Up_Right.Pixels[x, y] = pixel.FromArgb(0, 0, 255);
                                                Tunnel.Up_Left.Pixels[x, y] = pixel.FromArgb(0, 0, 255);
                                                Tunnel.Top.Pixels[x, y] = pixel.FromArgb(0, 0, 255);

                                            }
                                            else
                                            {
                                                Tunnel.Up_Right.Pixels[x, y] = pixel.FromArgb(128, 128, 0);
                                                Tunnel.Up_Left.Pixels[x, y] = pixel.FromArgb(128, 128, 0);
                                                Tunnel.Top.Pixels[x, y] = pixel.FromArgb(128, 128, 0);
                                            }

                                        }
                                    }

                               
                                alternate = !alternate;
                                UpdateTunnel();  //send serial commands
                                }
                            }

                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        public int UpdateTunnel()
        {
            // Tunnel
            //build string
            string SerialData = "*";
            //Tunnel.Left.Teensys_in_Panel[0]._serialPort.WriteLine(SerialData);
            //update left and right to depend on left and right hand ratios
            if (Tunnel.Left.Teensys_in_Panel[0]._serialPort.IsOpen)
            {
                Tunnel.Left.Teensys_in_Panel[0]._serialPort.WriteLine("*"); //send preamble
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 15; x++)
                    {
                        //SerialData = Tunnel.Left.Pixels[x, y].R.ToString("X")+ Tunnel.Left.Pixels[x, y].G.ToString("X")+ Tunnel.Left.Pixels[x, y].B.ToString("X");
                        SerialData = pixelTranslator.ToHtml(Tunnel.Left.Pixels[x, y]).Substring(1, 6);
                        //SerialData = "R" + Tunnel.Left.Pixels[x, y].R.ToString() + "G" + Tunnel.Left.Pixels[x, y].G.ToString() + "B" + Tunnel.Left.Pixels[x, y].B.ToString();
                        Tunnel.Left.Teensys_in_Panel[0]._serialPort.WriteLine(SerialData);

                    }
                }

                System.Threading.Thread.Sleep(10);
            }
            if (Tunnel.Left.Teensys_in_Panel.Count > 1)
            {
                if (Tunnel.Left.Teensys_in_Panel[0]._serialPort.IsOpen)
                {
                    Tunnel.Left.Teensys_in_Panel[1]._serialPort.WriteLine("XX"); //send preamble
                    for (int y = 9; y < 16; y++)
                    {
                        for (int x = 0; x < 150; x++)
                        {
                            SerialData = "R" + Tunnel.Left.Pixels[x, y].R.ToString() + "G" + Tunnel.Left.Pixels[x, y].G.ToString() + "B" + Tunnel.Left.Pixels[x, y].B.ToString();
                            Tunnel.Left.Teensys_in_Panel[1]._serialPort.WriteLine(SerialData);
                        }
                    }
                }
            }
            if (Tunnel.Left.Teensys_in_Panel[2]._serialPort.IsOpen)
            {
                Tunnel.Left.Teensys_in_Panel[2]._serialPort.WriteLine("XX"); //send preamble
                for (int y = 17; y < 24; y++)
                {
                    for (int x = 0; x < 150; x++)
                    {
                        SerialData = "R" + Tunnel.Left.Pixels[x, y].R.ToString() + "G" + Tunnel.Left.Pixels[x, y].G.ToString() + "B" + Tunnel.Left.Pixels[x, y].B.ToString();
                        Tunnel.Left.Teensys_in_Panel[1]._serialPort.WriteLine(SerialData);
                    }
                }
            }
            if (Tunnel.Left.Teensys_in_Panel[3]._serialPort.IsOpen)
            {
                Tunnel.Left.Teensys_in_Panel[3]._serialPort.WriteLine("XX"); //send preamble
                for (int y = 25; y < 32; y++)
                {
                    for (int x = 0; x < 150; x++)
                    {
                        SerialData = "R" + Tunnel.Left.Pixels[x, y].R.ToString() + "G" + Tunnel.Left.Pixels[x, y].G.ToString() + "B" + Tunnel.Left.Pixels[x, y].B.ToString();
                        Tunnel.Left.Teensys_in_Panel[1]._serialPort.WriteLine(SerialData);
                    }
                }
            }
            //go through each teensy that is enumerated 0-10 and then update the pixels that belong to it.

            return 0;
        }


        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        private void TEENSYS_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
