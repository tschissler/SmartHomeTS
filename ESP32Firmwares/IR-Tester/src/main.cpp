#include <Arduino.h>

#define IR_INPUT_PIN 23
#define IR_LED_PIN 19

int counter = 0;

void setup() {
  Serial.begin(115200);
  pinMode(IR_INPUT_PIN, INPUT);
  pinMode(IR_LED_PIN, OUTPUT);
  Serial.println("IR Receiver Test");
  delay(1000);
  Serial.println("Ready to receive IR signals.");
}

void loop() {
  if (digitalRead(IR_INPUT_PIN) == LOW) {
    Serial.print("1");
  }
  else {
    Serial.print("0");
  }

  counter++;

  if (counter >= 1000) {
    digitalWrite(IR_LED_PIN, HIGH);
  }
  else {
    digitalWrite(IR_LED_PIN, LOW);
  }
  if (counter >= 2000) {
    counter = 0;
  }
  delay(10); // Adjust delay as needed for your application
}

