using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.IO.Ports;
using WindowsInput; // For InputSimulator

class Program
{
    static SerialPort _serial;
    static ViGEmClient _client;
    static IXbox360Controller _x360;
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
        Console.WriteLine("Starting Arduino → Xbox360 bridge...");

        _client = new ViGEmClient();
        _x360 = _client.CreateXbox360Controller();
        _x360.Connect();
        Console.WriteLine("Virtual Xbox 360 controller connected.");

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
        _x360.Disconnect();
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

    // Map raw ADC -> normalized short (-32768 … +32767)
    static short MapStick(int raw, int center, int min, int max, bool invert = false)
    {
        int range = (raw >= center) ? (max - center) : (center - min);
        double norm = (raw - center) / (double)range;
        if (invert) norm = -norm;

        short val = (short)(norm * 32767);
        return val;
    }

    static void ProcessFrame(string csv)
    {
        var parts = csv.Split(',');

        bool A = parts[0] == "1";   // A
        bool B = parts[1] == "1";  // B
        bool X = parts[2] == "1";  // X
        bool Y = parts[3] == "1";// Y
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
        // Buttons
        //
        _x360.SetButtonState(Xbox360Button.A, A);
        _x360.SetButtonState(Xbox360Button.B, B);
        _x360.SetButtonState(Xbox360Button.X, X);
        _x360.SetButtonState(Xbox360Button.Y, Y);
        _x360.SetButtonState(Xbox360Button.Up, DU);
        _x360.SetButtonState(Xbox360Button.Down, DD);
        _x360.SetButtonState(Xbox360Button.Right, DR);
        _x360.SetButtonState(Xbox360Button.Left, DL);
        _x360.SetButtonState(Xbox360Button.LeftShoulder, left_l1);
        _x360.SetButtonState(Xbox360Button.RightShoulder, right_r1);
        _x360.SetButtonState(Xbox360Button.LeftThumb, JoyLBtnState);
        _x360.SetButtonState(Xbox360Button.RightThumb, JoyRBtnState);

        //
        // Triggers (0 … 255)
        //
        byte l2Value = left_l2 ? (byte)255 : (byte)0;
        byte r2Value = right_r2 ? (byte)255 : (byte)0;
        _x360.SetSliderValue(Xbox360Slider.LeftTrigger, l2Value);
        _x360.SetSliderValue(Xbox360Slider.RightTrigger, r2Value);

        //
        // Joysticks using MapStick
        //
        short leftX = MapStick(JoyLX, 2235, 0, 4095);
        short leftY = MapStick(JoyLY, 1916, 0, 4095, invert: true);
        short rightX = MapStick(JoyRX, 2250, 0, 4095);
        short rightY = MapStick(JoyRY, 1845, 0, 4095, invert: true);

        if (_mouseMode)
        {
            int mouseMoveX = (JoyRX - 2250) / 750;
            int mouseMoveY = (JoyRY - 1845) / 750;
            _inputSimulator.Mouse.MoveMouseBy(mouseMoveX, mouseMoveY);

            _x360.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _x360.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _x360.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            _x360.SetAxisValue(Xbox360Axis.RightThumbY, 0);

            if (JoyRBtnState && !prevBtnState)
                _inputSimulator.Mouse.LeftButtonDown();
            else if (!JoyRBtnState && prevBtnState)
                _inputSimulator.Mouse.LeftButtonUp();
            prevBtnState = JoyRBtnState;
        }
        else
        {
            _x360.SetAxisValue(Xbox360Axis.LeftThumbX, leftX);
            _x360.SetAxisValue(Xbox360Axis.LeftThumbY, leftY);
            _x360.SetAxisValue(Xbox360Axis.RightThumbX, rightX);
            _x360.SetAxisValue(Xbox360Axis.RightThumbY, rightY);

            if (prevBtnState)
            {
                _inputSimulator.Mouse.LeftButtonUp();
                prevBtnState = false;
            }
        }

        Console.WriteLine($"Parts: {string.Join(",", parts)}");
    }
}
