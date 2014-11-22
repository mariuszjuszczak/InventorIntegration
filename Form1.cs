using Inventor;
using Leap;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace InventorIntegration
{
    public partial class Form1: Form, ILeapEventDelegate
    {
        private InventorCamera oCamera = null;
        private Controller controller = null;
        private LeapEventListener listener = null;

        private bool circleEnabled = Properties.Settings.Default.circleEnabled;
        private bool swipeEnabled = Properties.Settings.Default.swipeEnabled;

        // Circle gesture settings
        private double minArc = Properties.Settings.Default.minArc;
        private double minRadius = Properties.Settings.Default.minRadius;
        
        // Swipe gesture settings
        private double minLength = Properties.Settings.Default.minLength;
        private double minVelocity = Properties.Settings.Default.minVelocity;

        private double sensitivity = Properties.Settings.Default.sensitivity;

        // DeadZones [mm]
        private double deadZoneX = Properties.Settings.Default.deadZoneX;
        private double deadZoneY = Properties.Settings.Default.deadZoneY;
        private double deadZoneZ = Properties.Settings.Default.deadZoneZ;

        // Height of 0 point
        private double centerHeight = Properties.Settings.Default.centerHeight;
        private double DEG_TO_RAD = Math.PI / 180; // convert DEG to RADIANS
        private bool isRightHanded = Properties.Settings.Default.isRightHanded;
        private double multiplier = 1000; // scale multiplier
        private int framesPassed = 0;
        public Form1()
        {
            InitializeComponent();
            oCamera = new InventorCamera();
            controller = new Controller();
            InitializeTexBox();
            AddToLog("Component initialization.");        
            Thread.Sleep(100); // wait for connection
            if (controller.Devices.IsEmpty)
            {
                AddToLog("ERROR: No connection to Leap Motion service.");
                AddToLog("ERROR: Connect device and restart application.");
                return;
            }
            else
            {
                AddToLog("Connected to Leap Motion service.");
                controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
                controller.EnableGesture(Gesture.GestureType.TYPE_CIRCLE);
                listener = new LeapEventListener(this);
                controller.SetPolicyFlags(Controller.PolicyFlag.POLICY_BACKGROUND_FRAMES);
                controller.AddListener(listener);
            }
            if (!oCamera.IsStarted())
                AddToLog("ERROR: Inventor instance not found.");
            else
                AddToLog("Iventor instance found. ");
            if (!oCamera.IsOpened())
                AddToLog("ERROR: Assembly, part or presentation document not found.");
            else
                AddToLog(oCamera.GetDocType() + " document found.");
        }

        delegate void LeapEventDelegate(string EventName);

        public void LeapEventNotification(string EventName)
        {
            if (!this.InvokeRequired)
            {
                switch (EventName)
                {
                    case "onInit":
                        break;
                    case "onConnect":
                        SetConfig();
                        break;
                    case "onFrame":
                        double scale = sensitivity / multiplier;
                        Frame frame;
                        Frame prevFrame;
                        frame = controller.Frame();
                        prevFrame = controller.Frame(1); // previous frame
                        double FPS = frame.CurrentFramesPerSecond;
                        double waitingTime = 2; // time to wait until action is taken
                        
                        HandList hands = frame.Hands; // 
                        label24.Text = hands.Count.ToString(); // show number of hands detected
                        GestureList gestures = frame.Gestures();
                        oCamera.GetCurrentCamera();
                        if (oCamera.IsStarted()) label21.Text = "ON"; else label21.Text = "OFF";
                        if (oCamera.IsOpened()) label22.Text = "ON"; else label22.Text = "OFF";
                        if (oCamera.IsStarted() == true && oCamera.IsOpened() == true)
                        {
                            // Check if both hands are in view    
                            if (hands.Count > 1)
                            {
                                Hand handLeft;
                                Hand handRight;
                                if (isRightHanded) // check if Right-handed
                                {
                                    handLeft = hands.Leftmost;
                                    handRight = hands.Rightmost;
                                }
                                else // swap hands
                                {
                                    handRight = hands.Leftmost;
                                    handLeft = hands.Rightmost;
                                }
                                // Reseting view using both hands
                                if (handLeft.GrabStrength < 0.2 && handRight.GrabStrength < 0.2) 
                                {
                                    // if hands are facing down
                                    if (Math.Abs(handLeft.PalmNormal.Roll) / DEG_TO_RAD > 140 && Math.Abs(handRight.PalmNormal.Roll) / DEG_TO_RAD > 140) 
                                        // count frames where hands are not moving
                                        if (Math.Abs(handLeft.RotationAngle(prevFrame, handLeft.PalmNormal)) < 30 * DEG_TO_RAD && Math.Abs(handRight.RotationAngle(prevFrame, handRight.PalmNormal)) < 30 * DEG_TO_RAD) 
                                            framesPassed++;
                                    // calculate time before taking action
                                    if (framesPassed > waitingTime * FPS) 
                                    {
                                        // reset view to default
                                        oCamera.ReturnHome();
                                        AddToLog("Camera set to HOME.");
                                        framesPassed = 0;
                                    }
                                }

                                // if left hand is open
                                if (handLeft.GrabStrength < 0.3)
                                {
                                    // check if there are any gestures
                                    if (gestures.Count > 0)
                                    {
                                        Gesture gesture = gestures[0];
                                        int direction = 0;
                                        switch (gesture.Type)
                                        {
                                            case Gesture.GestureType.TYPE_CIRCLE:
                                                if (circleEnabled)
                                                {
                                                    CircleGesture circle = new CircleGesture(gesture);
                                                    // Calculate clock direction using the angle between circle normal and pointable
                                                    if (circle.Pointable.Direction.AngleTo(circle.Normal) <= Math.PI / 2)
                                                        direction = 1; // clockwise
                                                    else
                                                        direction = -1; // counterclockwise
                                                    if (circle.State == Gesture.GestureState.STATE_STOP)
                                                        ScaleChange(0.1 * direction * circle.Progress);
                                                }
                                                break;

                                            case Gesture.GestureType.TYPE_SWIPE:
                                                if (swipeEnabled)
                                                {
                                                    SwipeGesture swipeGesture = new SwipeGesture(gesture);
                                                    // check if Horizontal or Vertical
                                                    bool isHorizontal = Math.Abs(swipeGesture.Direction.x) > Math.Abs(swipeGesture.Direction.y);
                                                    // wait until Swipe is stopped
                                                    if (swipeGesture.State == Gesture.GestureState.STATE_STOP)
                                                    {
                                                        string side = null;
                                                        if (isHorizontal) // horizontal
                                                        {
                                                            if (Math.Sign(swipeGesture.Direction.x) == -1)
                                                                side = "LEFT";
                                                            if (Math.Sign(swipeGesture.Direction.x) == 1)
                                                                side = "RIGHT";
                                                        }
                                                        else // vertical
                                                        {
                                                            if (Math.Sign(swipeGesture.Direction.y) == -1)
                                                                side = "DOWN";
                                                            if (Math.Sign(swipeGesture.Direction.y) == 1)
                                                                side = "UP";
                                                        }
                                                        oCamera.RotateCube(side);
                                                        AddToLog("View rotated by 90' " + side + " .");
                                                    }
                                                }
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                }
                            }
                            else // only 1 hand visible
                            {
                                Hand hand = hands.Frontmost;
                                bool properHand = false;

                                // Check default hand
                                if (isRightHanded) 
                                    properHand = hand.IsRight;
                                else
                                    properHand = hand.IsLeft;

                                if (properHand)
                                {
                                    Leap.Vector position = hand.PalmPosition;
                                    Leap.Vector normal = hand.PalmNormal;
                                    double x, y, z;

                                    int extendedFingers = 0;
                                    // check how many fingers are extended
                                    for (int f = 0; f < hand.Fingers.Count; f++)
                                    {
                                        Finger finger = hand.Fingers[f];
                                        if (finger.IsExtended) extendedFingers++;
                                    }

                                    if (hand.IsValid)
                                    {
                                        // Finger Tip position
                                        if (extendedFingers == 1) 
                                        {
                                            Finger finger = hand.Fingers.Frontmost;
                                            x = finger.TipPosition.x;
                                            y = finger.TipPosition.y - centerHeight;
                                        }
                                        // Hand position
                                        else
                                        {
                                            x = position.x;
                                            y = position.y - centerHeight;
                                        }
                                        z = position.z;
                                    }
                                    else
                                    {
                                        x = 0;
                                        y = 0;
                                        z = 0;
                                    }

                                    // Calculate movement with deadzones
                                    double roll = normal.Roll;
                                    double zX = (Math.Abs(x) - deadZoneX) * Math.Sign(x);
                                    double zY = (Math.Abs(y) - deadZoneY) * Math.Sign(y);
                                    double zZ = (Math.Abs(z) - deadZoneZ) * Math.Sign(z);
                                    int option = 0;

                                    if (Math.Abs(x) > deadZoneX && Math.Abs(y) <= deadZoneY) option = 1; // only X
                                    if (Math.Abs(x) <= deadZoneX && Math.Abs(y) > deadZoneY) option = 2; // only Y
                                    if (Math.Abs(x) > deadZoneX && Math.Abs(y) > deadZoneY) option = 3; // X and Y

                                    // Translate view
                                    if (extendedFingers == 1)
                                    {
                                        switch (option)
                                        {
                                            case 1:
                                                oCamera.TranslateView(zX * -scale, 0);
                                                break;
                                            case 2:
                                                oCamera.TranslateView(0, zY * -scale);
                                                break;
                                            case 3:
                                                oCamera.TranslateView(zX * -scale, zY * -scale);
                                                break;
                                        }
                                    }

                                    // Orbit view
                                    if (hand.GrabStrength < 0.65 && extendedFingers > 3)
                                    {
                                        switch (option)
                                        {
                                            case 1:
                                                oCamera.ChangeView(zX * -scale, 0, roll * -scale * 50, false);
                                                break;
                                            case 2:
                                                oCamera.ChangeView(0, zY * scale, roll * -scale * 50, false);
                                                break;
                                            case 3:
                                                oCamera.ChangeView(zX * -scale, zY * scale, roll * -scale * 50, false);
                                                break;
                                            default:
                                                oCamera.ChangeView(0, 0, roll * -scale * 50, false);
                                                break;
                                        }

                                        if (Math.Abs(z) > deadZoneZ)
                                        {
                                            oCamera.Zoom(zZ * -scale);
                                        }
                                    }
                                }
                            }
                        }

                        break;

                    case "onExit":
                        Thread.Sleep(1000);
                        break;
                }
                         
            }
            else
            {
                Invoke(new LeapEventDelegate(LeapEventNotification), new object[] { EventName });
            }
        }

        // Set controller config.
        void SetConfig()
        {
            controller.Config.SetFloat("Gesture.Swipe.MinLength", (float)minLength);
            controller.Config.SetFloat("Gesture.Swipe.MinVelocity", (float)minVelocity);
            controller.Config.SetFloat("Gesture.Circle.MinRadius", (float)minRadius);
            controller.Config.SetFloat("Gesture.Circle.MinArc", (float)(minArc*DEG_TO_RAD));
            controller.Config.Save();
        }

        // Change sensitivity
        private void ScaleChange(double s) 
        {
            sensitivity = sensitivity + s;
            if (sensitivity < 0.1) sensitivity = 0.1;
            sensitivity = Math.Round(sensitivity, 2);
            textBox1.Text = sensitivity.ToString();
            if (s > 0)
                AddToLog("Sensitivity increased by " + Math.Round(s,2) + ".");
            else
                AddToLog("Sensitivity decreased by " + Math.Abs(Math.Round(s,2)) + ".");

            listBoxLog.SelectedIndex = listBoxLog.Items.Count - 1;
        }

        // Disconnect on closing
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            
            if (e.CloseReason == CloseReason.WindowsShutDown) return;

            // Confirm user wants to close
            switch (MessageBox.Show(this, "Are you sure you want to close?", "Closing", MessageBoxButtons.YesNo))
            {
                case DialogResult.No:
                    e.Cancel = true;
                    break;
                default:
                    //SaveSettings();
                    if (listener != null)
                        controller.RemoveListener(listener);

                    controller.Dispose();
                    break;
            }
        }

        // Event log
        private void AddToLog(String message)
        {
            listBoxLog.Items.Insert(listBoxLog.Items.Count, DateTime.Now.ToString("[HH:mm:ss]", System.Globalization.DateTimeFormatInfo.InvariantInfo) + ": " + message);
        }

        // Load up values into textboxes
        private void InitializeTexBox()
        {
            if (oCamera.IsStarted()) label21.Text = "ON"; else label21.Text = "OFF";
            if (oCamera.IsOpened()) label22.Text = "ON"; else label22.Text = "OFF";
            rbRight.Checked = isRightHanded;
            rbLeft.Checked = !isRightHanded;
            checkBoxSwipe.Checked = swipeEnabled;
            checkBoxCircle.Checked = circleEnabled;
            groupBox4.Enabled = swipeEnabled;
            groupBox5.Enabled = circleEnabled;
            tbDeadZoneX.Text = deadZoneX.ToString();
            tbDeadZoneY.Text = deadZoneY.ToString();
            tbDeadZoneZ.Text = deadZoneZ.ToString();
            tbCenter.Text = centerHeight.ToString();
            textBox1.Text = Math.Round(sensitivity,2).ToString();
            tbMinArc.Text = minArc.ToString();
            tbMinRadius.Text = minRadius.ToString();
            tbMinVelocity.Text = minVelocity.ToString();
            tbMinLength.Text = minLength.ToString();
        }

        // Check if values are correct (unsigned and no letters)
        private double TextBoxCheck(string textbox, double value, string s)
        {
            double output;
            if (!Double.TryParse(textbox, out output))
            {
                AddToLog("[ERROR]: Incorrect " + s + " value.");
                return value;
            }
            else
            {
                if (output < 0)
                {
                    AddToLog("[ERROR]: Negative " + s + " value detected. Converting to unsigned.");
                    output = Math.Abs(output);
                }
                return output;
            }
        }

        private void ParseTextBox() // get values from textboxes
        {
            sensitivity = TextBoxCheck(textBox1.Text, sensitivity, "sensitivity");
            deadZoneX = TextBoxCheck(tbDeadZoneX.Text, deadZoneX, "deadZoneX");
            deadZoneY = TextBoxCheck(tbDeadZoneY.Text, deadZoneY, "deadZoneY");
            deadZoneZ = TextBoxCheck(tbDeadZoneZ.Text, deadZoneZ, "deadZoneZ");
            centerHeight = TextBoxCheck(tbCenter.Text, centerHeight, "centerHeight");
            minArc = TextBoxCheck(tbMinArc.Text,  minArc, "minArc");
            minRadius = TextBoxCheck(tbMinRadius.Text, minRadius, "minRadius");
            minVelocity = TextBoxCheck(tbMinVelocity.Text, minVelocity, "minVelocity");
            minLength = TextBoxCheck(tbMinLength.Text, minLength, "minLength");
            //InitializeTexBox();
        }

        private void SaveSettings() // save settings to file
        {
            Properties.Settings.Default.deadZoneX = deadZoneX;
            Properties.Settings.Default.deadZoneY = deadZoneY;
            Properties.Settings.Default.deadZoneZ = deadZoneZ;
            Properties.Settings.Default.sensitivity = sensitivity;
            Properties.Settings.Default.centerHeight = centerHeight;
            Properties.Settings.Default.minArc = minArc;
            Properties.Settings.Default.minRadius = minRadius;
            Properties.Settings.Default.minLength = minLength;
            Properties.Settings.Default.minVelocity = minVelocity;
            Properties.Settings.Default.swipeEnabled = swipeEnabled;
            Properties.Settings.Default.circleEnabled = circleEnabled;
            Properties.Settings.Default.isRightHanded = isRightHanded;
            Properties.Settings.Default.Save();
        }

        private void checkBoxSwipe_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxSwipe.Checked == true)
                groupBox4.Enabled = true;
            else
                groupBox4.Enabled = false;
        }

        private void checkBoxCircle_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxCircle.Checked == true)
                groupBox5.Enabled = true;
            else
                groupBox5.Enabled = false;
        }

        private void buttonApply_Click(object sender, EventArgs e) // apply and save settings
        {
            ParseTextBox();

            if (checkBoxCircle.Checked == true) 
                circleEnabled = true; 
            else 
                circleEnabled = false;

            if (checkBoxSwipe.Checked == true) 
                swipeEnabled = true; 
            else 
                swipeEnabled = false;

            if (rbRight.Checked == true) 
                isRightHanded = true;
            else
                isRightHanded = false;

            SetConfig();
            SaveSettings();
            AddToLog("Settings saved.");
        }

    }
}

