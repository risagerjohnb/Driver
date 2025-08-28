# Arduino Pro Mini Code

// Right side (action buttons)
const int PIN_A = 2;   // rBtnDown
const int PIN_B = 3;   // rBtnRight
const int PIN_Y = 4;   // rBtnUp
const int PIN_X = 5;   // rBtnLeft

// Left side (D-Pad)
const int PIN_DD = 6;  // lBtnDown
const int PIN_DR = 7;  // lBtnRight
const int PIN_DU = 8;  // lBtnUp
const int PIN_DL = 9;  // lBtnLeft

const int JOY_X = A0;
const int JOY_Y = A1; 
const int BUTTON_PIN = 10;

const int JOY_X2 = A2;
const int JOY_Y2 = A3;

int readButton(int pin) {
  return digitalRead(pin) == LOW ? 1 : 0; // pressed=1 (using INPUT_PULLUP)
}

void setup() {
  Serial.begin(115200);

  pinMode(PIN_A, INPUT_PULLUP);
  pinMode(PIN_B, INPUT_PULLUP);
  pinMode(PIN_X, INPUT_PULLUP);
  pinMode(PIN_Y, INPUT_PULLUP);

  pinMode(PIN_DU, INPUT_PULLUP);
  pinMode(PIN_DD, INPUT_PULLUP);
  pinMode(PIN_DL, INPUT_PULLUP);
  pinMode(PIN_DR, INPUT_PULLUP);
  pinMode(BUTTON_PIN, INPUT_PULLUP);
}

void loop() {
  int JOYstick_X = analogRead(JOY_X);
  int JOYstick_Y = analogRead(JOY_Y);
  int btnState = digitalRead(BUTTON_PIN);
  int JOYstick_X2 = analogRead(JOY_X2);
  int JOYstick_Y2 = analogRead(JOY_Y2);

  int A  = readButton(PIN_A);
  int B  = readButton(PIN_B);
  int X  = readButton(PIN_X);
  int Y  = readButton(PIN_Y);

  int DU = readButton(PIN_DU);
  int DD = readButton(PIN_DD);
  int DL = readButton(PIN_DL);
  int DR = readButton(PIN_DR);

  // Send CSV line
  Serial.print(A); Serial.print(",");
  Serial.print(B); Serial.print(",");
  Serial.print(X); Serial.print(",");
  Serial.print(Y); Serial.print(",");
  Serial.print(DU); Serial.print(",");
  Serial.print(DD); Serial.print(",");
  Serial.print(DL); Serial.print(",");
  Serial.print(DR); Serial.print(",");
  Serial.print(JOYstick_X); Serial.print(",");
  Serial.print(JOYstick_Y); Serial.print(",");
  Serial.print(btnState); Serial.print(",");
  Serial.print(JOYstick_X2); Serial.print(",");
  Serial.println(JOYstick_Y2);

  delay(5); // ~200Hz
}
