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
        bool A = parts[0] == "1"; // Cross button
        bool B = parts[1] == "1"; // Circle button
        bool X = parts[2] == "1"; // Square button
        bool Y = parts[3] == "1"; // Triangle button
        bool DU = parts[4] == "1"; // D-pad Up
        bool DD = parts[5] == "1"; // D-pad Down
        bool DL = parts[6] == "1"; // D-pad Left
        bool DR = parts[7] == "1"; // D-pad Right
        int JoyX = int.Parse(parts[8]); // Left joystick X
        int JoyY = int.Parse(parts[9]); // Left joystick Y
        bool btnState = parts[10] == "0"; // Right thumb stick pressed (Touchpad click)
        int JoyX2 = int.Parse(parts[11]); // Right joystick X
        int JoyY2 = int.Parse(parts[12]); // Right joystick Y

        // Map buttons to DS4 controller
        _ds4.SetButtonState(DualShock4Button.Cross, A);
        _ds4.SetButtonState(DualShock4Button.Circle, B);
        _ds4.SetButtonState(DualShock4Button.Square, X);
        _ds4.SetButtonState(DualShock4Button.Triangle, Y);

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

        // Map left joystick (convert to byte)
        byte leftX = (byte)(((JoyX - 512) / 512.0 * 255) + 128);
        byte leftY = (byte)(-((JoyY - 512) / 512.0 * 255) + 128);

        // Map right joystick (convert to byte)
        byte rightX = (byte)(((JoyX2 - 512) / 512.0 * 255) + 128);
        byte rightY = (byte)(-((JoyY2 - 512) / 512.0 * 255) + 128);

        // Set axis values for DS4
        _ds4.SetAxisValue(DualShock4Axis.LeftThumbX, leftX);
        _ds4.SetAxisValue(DualShock4Axis.LeftThumbY, leftY);
        _ds4.SetAxisValue(DualShock4Axis.RightThumbX, rightX);
        _ds4.SetAxisValue(DualShock4Axis.RightThumbY, rightY);

        // Handle left mouse button press (only trigger on rising edge)
        if (btnState && !prevBtnState)
        {
            _inputSimulator.Mouse.LeftButtonDown();
        }
        else if (!btnState && prevBtnState)
        {
            _inputSimulator.Mouse.LeftButtonUp();
        }
        prevBtnState = btnState; // Update previous state

        // Map RIGHT joystick to mouse movement
        int mouseMoveX = (JoyX2 - 512) / 150; // Adjust divisor for sensitivity
        int mouseMoveY = (JoyY2 - 512) / 150; // Adjust divisor for sensitivity
        _inputSimulator.Mouse.MoveMouseBy(mouseMoveX, mouseMoveY);

        // Log for debugging
        Console.WriteLine($"A:{A} B:{B} X:{X} Y:{Y} DU:{DU} DD:{DD} DL:{DL} DR:{DR} JoyX:{JoyX} JoyY:{JoyY} Btn:{btnState} JoyX2:{JoyX2} JoyY2:{JoyY2}");
    }

}
