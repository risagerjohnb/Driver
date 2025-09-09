using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using System.IO.Ports;
using WindowsInput; // For InputSimulator

class Program
{
    static SerialPort _serial;
    static ViGEmClient _client;
    static IDualShock4Controller _ds4;
    static volatile bool _running = true;
    static string ComPort = "";
    const int Baud = 115200;
    static InputSimulator _inputSimulator = new InputSimulator();
    static bool prevBtnState = false;
    static bool _mouseMode = false;
    static bool prevGameBtnState = false;

    static void Main()
    {
        Console.WriteLine("Enter the ComPort: ");
        ComPort = Console.ReadLine().ToUpper();
        Console.WriteLine("Starting Arduino → DS4 bridge...");

        _client = new ViGEmClient();
        _ds4 = _client.CreateDualShock4Controller();
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

    // Map raw ADC -> normalized short -> DS4 axis (0-255)
    static byte MapStick(int raw, int center, int min, int max, bool invert = false)
    {
        int range = (raw >= center) ? (max - center) : (center - min);
        double norm = (raw - center) / (double)range;
        if (invert) norm = -norm;

        short val = (short)(norm * 32767); // -32768 … +32767
        int ds4 = (val + 32768) / 257;     // convert to 0 … 255
        return (byte)Math.Clamp(ds4, 0, 255);
    }

    static void ProcessFrame(string csv)
    {
        var parts = csv.Split(',');

        bool Cross = parts[0] == "1";
        bool Circle = parts[1] == "1";
        bool Square = parts[2] == "1";
        bool Triangle = parts[3] == "1";
        bool DU = parts[4] == "1";
        bool DD = parts[5] == "1";
        bool DL = parts[6] == "1";
        bool DR = parts[7] == "1";
        bool JoyLBtnState = parts[8] == "1";
        bool JoyRBtnState = parts[9] == "1";
        int JoyLX = int.Parse(parts[10]);
        int JoyLY = int.Parse(parts[11]);
        int JoyRX = int.Parse(parts[12]);
        int JoyRY = int.Parse(parts[13]);
        bool gameBtn = parts[14] == "1";
        bool startBtn = parts[15] == "1";
        bool left_l1 = parts[16] == "1";
        bool left_l2 = parts[17] == "1";
        bool right_r1 = parts[18] == "1";
        bool right_r2 = parts[19] == "1";

        // Toggle mouse mode
        if (gameBtn && !prevGameBtnState)
        {
            _mouseMode = !_mouseMode;
            Console.WriteLine($"Mouse mode: {_mouseMode}");
            if (!_mouseMode)
            {
                _inputSimulator.Mouse.LeftButtonUp();
                prevBtnState = false;
            }
        }
        prevGameBtnState = gameBtn;

        //
        // D-Pad
        //
        DualShock4DPadDirection dpadDirection = DualShock4DPadDirection.None;
        if (DU)
        {
            if (DL) dpadDirection = DualShock4DPadDirection.Northwest;
            else if (DR) dpadDirection = DualShock4DPadDirection.Northeast;
            else dpadDirection = DualShock4DPadDirection.North;
        }
        else if (DD)
        {
            if (DL) dpadDirection = DualShock4DPadDirection.Southwest;
            else if (DR) dpadDirection = DualShock4DPadDirection.Southeast;
            else dpadDirection = DualShock4DPadDirection.South;
        }
        else if (DL) dpadDirection = DualShock4DPadDirection.West;
        else if (DR) dpadDirection = DualShock4DPadDirection.East;

        _ds4.SetDPadDirection(dpadDirection);

        //
        // Buttons
        //
        _ds4.SetButtonState(DualShock4Button.Cross, Cross);
        _ds4.SetButtonState(DualShock4Button.Circle, Circle);
        _ds4.SetButtonState(DualShock4Button.Square, Square);
        _ds4.SetButtonState(DualShock4Button.Triangle, Triangle);
        _ds4.SetButtonState(DualShock4Button.ShoulderLeft, left_l1);
        _ds4.SetButtonState(DualShock4Button.ShoulderRight, right_r1);
        _ds4.SetButtonState(DualShock4Button.ThumbLeft, JoyLBtnState);
        _ds4.SetButtonState(DualShock4Button.ThumbRight, JoyRBtnState);
        _ds4.SetButtonState(DualShock4Button.Options, startBtn);

        //
        // Triggers
        //
        byte l2Value = left_l2 ? (byte)255 : (byte)0;
        byte r2Value = right_r2 ? (byte)255 : (byte)0;
        _ds4.SetSliderValue(DualShock4Slider.LeftTrigger, l2Value);
        _ds4.SetSliderValue(DualShock4Slider.RightTrigger, r2Value);

        //
        // Joysticks using MapStick
        //
        byte leftX = MapStick(JoyLX, 2235, 0, 4095);
        byte leftY = MapStick(JoyLY, 1916, 0, 4095, invert: false); // invert Y
        byte rightX = MapStick(JoyRX, 2250, 0, 4095);
        byte rightY = MapStick(JoyRY, 1845, 0, 4095, invert: true);

        if (_mouseMode)
        {
            int mouseMoveX = (JoyRX - 2250) / 150;
            int mouseMoveY = (JoyRY - 1845) / 150;
            _inputSimulator.Mouse.MoveMouseBy(mouseMoveX, mouseMoveY);

            _ds4.SetAxisValue(DualShock4Axis.LeftThumbX, 128);
            _ds4.SetAxisValue(DualShock4Axis.LeftThumbY, 128);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbX, 128);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbY, 128);

            if (JoyRBtnState && !prevBtnState)
                _inputSimulator.Mouse.LeftButtonDown();
            else if (!JoyRBtnState && prevBtnState)
                _inputSimulator.Mouse.LeftButtonUp();
            prevBtnState = JoyRBtnState;
        }
        else
        {
            _ds4.SetAxisValue(DualShock4Axis.LeftThumbX, leftX);
            _ds4.SetAxisValue(DualShock4Axis.LeftThumbY, leftY);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbX, rightX);
            _ds4.SetAxisValue(DualShock4Axis.RightThumbY, rightY);

            if (prevBtnState)
            {
                _inputSimulator.Mouse.LeftButtonUp();
                prevBtnState = false;
            }
        }

        Console.WriteLine($"Parts: {string.Join(",", parts)}");
    }
}
