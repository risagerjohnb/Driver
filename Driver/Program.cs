using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System.IO.Ports;
using WindowsInput; // For InputSimulator

class Program
{
    static SerialPort _serial;
    static ViGEmClient _client;
    static IDualShock4Controller _ds4; // Use DS4 controller instead of Xbox360
    static volatile bool _running = true;
    static string ComPort = "";  // <-- change to your COM port
    const int Baud = 115200;
    static InputSimulator _inputSimulator = new InputSimulator();
    static bool prevBtnState = false; // Track previous button state
    static bool _mouseMode = false;
    static bool prevGameBtnState = false;

    static void Main()
    {
        Console.WriteLine("Enter the ComPort: ");
        ComPort = Console.ReadLine().ToUpper();
        Console.WriteLine("Starting Arduino → DS4 bridge...");
        _client = new ViGEmClient();
        _ds4 = _client.CreateDualShock4Controller(); // Create DS4 controller
        _ds4.Connect();
        Console.WriteLine("Virtual DualShock 4 controller connected.");
        _serial = new SerialPort(ComPort, Baud)
        {
            NewLine = "\n",
            ReadTimeout = 100
        };
        try
        {
            _serial.Open();
            Console.WriteLine($"Serial open on {ComPort}@{Baud}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open {ComPort}: {ex.Message}");
            return;
        }
        var readThread = new Thread(ReadLoop) { IsBackground = true };
        readThread.Start();
        Console.WriteLine("Press Enter to quit.");
        Console.ReadLine();
        _running = false;
        readThread.Join();
        _serial.Close();
        _ds4.Disconnect();
        Console.WriteLine("Stopped.");
    }

    static void ReadLoop()
    {
        while (_running)
        {
            try
            {
                string line = _serial.ReadLine();
                ProcessFrame(line.Trim());
            }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial error: {ex.Message}");
            }
        }
    }

    static void ProcessFrame(string csv)
    {
        var parts = csv.Split(',');
        bool Cross = parts[0] == "1"; // Cross button
        bool Circle = parts[1] == "1"; // Circle button
        bool Square = parts[2] == "1"; // Square button
        bool Triangle = parts[3] == "1"; // Triangle button
        bool DU = parts[4] == "1"; // D-pad Up
        bool DD = parts[5] == "1"; // D-pad Down
        bool DL = parts[6] == "1"; // D-pad Left
        bool DR = parts[7] == "1"; // D-pad Right
        bool JoyLBtnState = parts[8] == "1";
        bool JoyRBtnState = parts[9] == "1"; // Right thumb stick pressed (Touchpad click)
        int JoyLX = int.Parse(parts[10]);   // Left joystick X
        int JoyLY = int.Parse(parts[11]);   // Left joystick Y
        int JoyRX = int.Parse(parts[12]); // Right joystick X
        int JoyRY = int.Parse(parts[13]); // Right joystick Y
        bool gameBtn = parts[14] == "1";
        bool startBtn = parts[15] == "1";
        bool left_l1 = parts[16] == "1";
        bool left_l2 = parts[17] == "1";
        bool right_r1 = parts[18] == "1";
        bool right_r2 = parts[19] == "1";

        // Toggle mouse mode on button press
        if (gameBtn && !prevGameBtnState)
        {
            _mouseMode = !_mouseMode;
            Console.WriteLine($"Mouse mode: {_mouseMode}");

            // Reset mouse button state when switching modes
            if (!_mouseMode)
            {
                _inputSimulator.Mouse.LeftButtonUp();
                prevBtnState = false;
            }
        }
        prevGameBtnState = gameBtn;

        // Determine D-pad direction
        DualShock4DPadDirection dpadDirection = DualShock4DPadDirection.None;
        if (DU)
        {
            if (DL)
                dpadDirection = DualShock4DPadDirection.Northwest;
            else if (DR)
                dpadDirection = DualShock4DPadDirection.Northeast;
            else
                dpadDirection = DualShock4DPadDirection.North;
        }
        else if (DD)
        {
            if (DL)
                dpadDirection = DualShock4DPadDirection.Southwest;
            else if (DR)
                dpadDirection = DualShock4DPadDirection.Southeast;
            else
                dpadDirection = DualShock4DPadDirection.South;
        }
        else if (DL)
            dpadDirection = DualShock4DPadDirection.West;
        else if (DR)
            dpadDirection = DualShock4DPadDirection.East;

        // Set D-pad direction
        _ds4.SetDPadDirection(dpadDirection);

        // Map buttons to DS4 controller
        _ds4.SetButtonState(DualShock4Button.Cross, Cross);
        _ds4.SetButtonState(DualShock4Button.Circle, Circle);
        _ds4.SetButtonState(DualShock4Button.Square, Square);
        _ds4.SetButtonState(DualShock4Button.Triangle, Triangle);
        _ds4.SetButtonState(DualShock4Button.ShoulderLeft, left_l1);
        _ds4.SetButtonState(DualShock4Button.ShoulderRight, right_r1);

        // Map L2 and R2 as triggers
        byte l2Value = left_l2 ? (byte)255 : (byte)0;
        byte r2Value = right_r2 ? (byte)255 : (byte)0;
        _ds4.SetSliderValue(DualShock4Slider.LeftTrigger, l2Value);
        _ds4.SetSliderValue(DualShock4Slider.RightTrigger, r2Value);

        int joyLXRest = 2235;
        int joyLYRest = 1916;
        int centeredLX = JoyLX - joyLXRest;
        int centeredLY = JoyLY - joyLYRest;
        byte leftX = (byte)((centeredLX / 10.0) + 150); // Adjust divisor for sensitivity
        byte leftY = (byte)((centeredLY / 10.0) + 150); // Adjust divisor for sensitivity

        // Center and scale right joystick values
        int joyRXRest = 2250;
        int joyRYRest = 1845;
        int centeredRX = JoyRX - joyRXRest;
        int centeredRY = JoyRY - joyRYRest;
        byte rightX = (byte)((centeredRX / 10.0) + 128); // Adjust divisor for sensitivity
        byte rightY = (byte)((centeredRY / 10.0) + 128); // Adjust divisor for sensitivity

        // Clamp values to 0-255
        rightX = (byte)Math.Max(0, Math.Min(255, (int)rightX));
        rightY = (byte)Math.Max(0, Math.Min(255, (int)rightY));
        leftX = (byte)Math.Max(0, Math.Min(255, (int)leftX));
        leftY = (byte)Math.Max(0, Math.Min(255, (int)leftY));

        if (_mouseMode)
        {
            // Mouse control
            int mouseMoveX = centeredRX / 150; // Adjust divisor for sensitivity
            int mouseMoveY = centeredRY / 150; // Adjust divisor for sensitivity
            _inputSimulator.Mouse.MoveMouseBy(mouseMoveX, mouseMoveY);

            // Neutralize gamepad
            _ds4.SetAxisValue(DualShock4Axis.RightThumbX, 128);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbY, 128);

            _ds4.SetAxisValue(DualShock4Axis.LeftThumbX, 128);
            _ds4.SetAxisValue(DualShock4Axis.LeftThumbY, 128);
            // Left mouse click
            if (JoyRBtnState && !prevBtnState)
                _inputSimulator.Mouse.LeftButtonDown();
            else if (!JoyRBtnState && prevBtnState)
                _inputSimulator.Mouse.LeftButtonUp();
            prevBtnState = JoyRBtnState;
        }
        else
        {
            // Gamepad control
            _ds4.SetAxisValue(DualShock4Axis.RightThumbX, rightX);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbY, rightY);

            _ds4.SetAxisValue(DualShock4Axis.LeftThumbX, leftX);
            _ds4.SetAxisValue(DualShock4Axis.LeftThumbY, leftY);
            // Release mouse button when exiting mouse mode
            if (prevBtnState)
            {
                _inputSimulator.Mouse.LeftButtonUp();
                prevBtnState = false;
            }
        }

        Console.WriteLine($"Parts: {string.Join(",", parts)}");
    }


}
