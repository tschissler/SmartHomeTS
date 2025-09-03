#include <Arduino.h>


#define RELAIS_1 15  
#define RELAIS_2 0
#define RELAIS_3 4
#define RELAIS_4 16
#define RELAIS_5 17
#define RELAIS_6 5
#define RELAIS_7 18
#define RELAIS_8 19

#define waitTime 100

void setup() {
  Serial.begin(115200);
  
  pinMode(RELAIS_1, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_2, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_3, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_4, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_5, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_6, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_7, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_8, OUTPUT_OPEN_DRAIN);
}

void loop() {
  Serial.println("Switching RELAIS 1 ON");
  digitalWrite(RELAIS_1, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 2 ON");
  digitalWrite(RELAIS_2, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 3 ON");
  digitalWrite(RELAIS_3, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 4 ON");
  digitalWrite(RELAIS_4, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 5 ON");
  digitalWrite(RELAIS_5, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 6 ON");
  digitalWrite(RELAIS_6, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 7 ON");
  digitalWrite(RELAIS_7, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 8 ON");
  digitalWrite(RELAIS_8, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 1 OFF");
  digitalWrite(RELAIS_1, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 2 OFF");
  digitalWrite(RELAIS_2, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 3 OFF");
  digitalWrite(RELAIS_3, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 4 OFF");
  digitalWrite(RELAIS_4, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 5 OFF");
  digitalWrite(RELAIS_5, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 6 OFF");
  digitalWrite(RELAIS_6, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 7 OFF");
  digitalWrite(RELAIS_7, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 8 OFF");
  digitalWrite(RELAIS_8, LOW);
  delay(waitTime);
}

