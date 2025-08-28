using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.IO.Ports;
using WindowsInput; // For InputSimulator

class Program
{
    static SerialPort _serial;
    static ViGEmClient _client;
    static IXbox360Controller _xbox;
    static volatile bool _running = true;
    static string ComPort = "COM9";  // <-- change to your COM port
    const int Baud = 115200;
    static InputSimulator _inputSimulator = new InputSimulator();

    static void Main()
    {
        Console.WriteLine("Starting Arduino → XInput bridge...");
        _client = new ViGEmClient();
        _xbox = _client.CreateXbox360Controller();
        _xbox.Connect();
        Console.WriteLine("Virtual Xbox 360 controller connected.");
        Console.WriteLine("Enter the ComPort: ");
        ComPort = Console.ReadLine();
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
        _xbox.Disconnect();
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
        bool A = parts[0] == "1";
        bool B = parts[1] == "1";
        bool X = parts[2] == "1";
        bool Y = parts[3] == "1";
        bool DU = parts[4] == "1";
        bool DD = parts[5] == "1";
        bool DL = parts[6] == "1";
        bool DR = parts[7] == "1";
        int JoyX = int.Parse(parts[8]);
        int JoyY = int.Parse(parts[9]);
        bool btnState = parts[10] == "0";
        int JoyX2 = int.Parse(parts[11]); // Right joystick X
        int JoyY2 = int.Parse(parts[12]); // Right joystick Y

        // Map buttons to Xbox controller
        _xbox.SetButtonState(Xbox360Button.A, A);
        _xbox.SetButtonState(Xbox360Button.B, B);
        _xbox.SetButtonState(Xbox360Button.X, X);
        _xbox.SetButtonState(Xbox360Button.Y, Y);
        _xbox.SetButtonState(Xbox360Button.Up, DU);
        _xbox.SetButtonState(Xbox360Button.Down, DD);
        _xbox.SetButtonState(Xbox360Button.Left, DL);
        _xbox.SetButtonState(Xbox360Button.Right, DR);
        _xbox.SetButtonState(Xbox360Button.LeftThumb, btnState);
        _xbox.SetButtonState(Xbox360Button.RightThumb, btnState);

        // Map RIGHT joystick to mouse movement
        int mouseMoveX = (JoyX2 - 512) / 150; // Adjust divisor for sensitivity
        int mouseMoveY = (JoyY2 - 512) / 150; // Adjust divisor for sensitivity
        _inputSimulator.Mouse.MoveMouseBy(mouseMoveX, mouseMoveY);

        // Log for debugging
        Console.WriteLine($"A:{A} B:{B} X:{X} Y:{Y} DU:{DU} DD:{DD} DL:{DL} DR:{DR} JoyX:{JoyX} JoyY:{JoyY} Btn:{btnState} JoyX2:{JoyX2} JoyY2:{JoyY2}");
    }
}
