#include "BluetoothSerial.h"
#include <Wire.h>
#include <math.h>
#include <SparkFunADXL313.h>

BluetoothSerial SerialBT;

// Gamepad pins (ESP32 GPIOs)
const int PIN_X = 15, PIN_O = 2, PIN_Firkant = 4, PIN_Trekant = 0;
const int PIN_DU = 5, PIN_DD = 16, PIN_DL = 18, PIN_DR = 17;
const int joyStickBtnLeft = 19;
const int joyStickBtnRight = 23;

// Joystick analog pins (ESP32 GPIOs)
const int JOY_X = 36, JOY_Y = 39;
const int JOY_X2 = 34, JOY_Y2 = 35;

const int gameBtn = 27, startBtn = 14;
const int left_l1 = 32, left_l2 = 33;
const int right_r1 = 25, right_r2 = 26;

int readButton(int pin) { return digitalRead(pin) == LOW ? 1 : 0; }

void rotateJoystick(int rawX, int rawY, float angle, int &outX, int &outY) {
    int x = rawX - 2048;
    int y = rawY - 2048;
    float xr = x * cos(angle) - y * sin(angle);
    float yr = x * sin(angle) + y * cos(angle);
    outX = constrain((int)xr + 2048, 0, 4095);
    outY = constrain((int)yr + 2048, 0, 4095);
}

void setup() {
    Serial.begin(115200);

    // Initialize I2C
    Wire.begin(21, 22);  // ESP32 SDA=21, SCL=22

    // Configure ESP32 buttons
    pinMode(PIN_X, INPUT_PULLUP);
    pinMode(PIN_O, INPUT_PULLUP);
    pinMode(PIN_Firkant, INPUT_PULLUP);
    pinMode(PIN_Trekant, INPUT_PULLUP);
    pinMode(PIN_DU, INPUT_PULLUP);
    pinMode(PIN_DD, INPUT_PULLUP);
    pinMode(PIN_DL, INPUT_PULLUP);
    pinMode(PIN_DR, INPUT_PULLUP);
    pinMode(joyStickBtnLeft, INPUT_PULLUP);
    pinMode(joyStickBtnRight, INPUT_PULLUP);
    pinMode(startBtn, INPUT_PULLUP);
    pinMode(gameBtn, INPUT_PULLUP);
    pinMode(left_l1, INPUT_PULLUP);
    pinMode(left_l2, INPUT_PULLUP);
    pinMode(right_r1, INPUT_PULLUP);
    pinMode(right_r2, INPUT_PULLUP);
    
    SerialBT.begin("Nillers");  // Bluetooth Serial
}

void loop() {
    // --- Read joysticks ---
    int lx = analogRead(JOY_X);
    int ly = analogRead(JOY_Y);
    int rx = analogRead(JOY_X2);
    int ry = analogRead(JOY_Y2);

    int rotLX, rotLY, rotRX, rotRY;
    rotateJoystick(lx, ly, M_PI / 2, rotLX, rotLY);
    rotateJoystick(rx, ry, M_PI / 2, rotRX, rotRY);

    // --- Read ESP32 buttons ---
    String data =
        String(readButton(PIN_X)) + "," + String(readButton(PIN_O)) + "," +
        String(readButton(PIN_Firkant)) + "," + String(readButton(PIN_Trekant)) + "," +
        String(readButton(PIN_DU)) + "," + String(readButton(PIN_DD)) + "," +
        String(readButton(PIN_DL)) + "," + String(readButton(PIN_DR)) + "," +
        String(readButton(joyStickBtnLeft)) + "," + String(readButton(joyStickBtnRight)) + "," +
        String(rotLX) + "," + String(rotLY) + "," +
        String(rotRX) + "," + String(rotRY) + "," +
        String(readButton(gameBtn)) + "," + String(readButton(startBtn)) + "," +
        String(readButton(left_l1)) + "," + String(readButton(left_l2)) + "," +
        String(readButton(right_r1)) + "," + String(readButton(right_r2));

    SerialBT.println(data);
    Serial.println(data);

    delay(20);  // Small delay to reduce latency
}
