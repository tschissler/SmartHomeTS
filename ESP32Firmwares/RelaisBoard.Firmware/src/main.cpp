#include <Arduino.h>


#define RELAIS_1 32  
#define RELAIS_3 33
#define RELAIS_5 27
#define RELAIS_7 14
#define RELAIS_9 23
#define RELAIS_11 22

#define waitTime 100

void setup() {
  Serial.begin(115200);
  
  pinMode(RELAIS_1, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_3, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_5, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_7, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_9, OUTPUT_OPEN_DRAIN);
  pinMode(RELAIS_11, OUTPUT_OPEN_DRAIN);
}

void loop() {
  Serial.println("Switching RELAIS 1 ON");
  digitalWrite(RELAIS_1, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 3 ON");
  digitalWrite(RELAIS_3, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 5 ON");
  digitalWrite(RELAIS_5, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 7 ON");
  digitalWrite(RELAIS_7, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 9 ON");
  digitalWrite(RELAIS_9, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 11 ON");
  digitalWrite(RELAIS_11, HIGH);
  delay(waitTime);
  Serial.println("Switching RELAIS 1 OFF");
  digitalWrite(RELAIS_1, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 3 OFF");
  digitalWrite(RELAIS_3, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 5 OFF");
  digitalWrite(RELAIS_5, LOW);  
  delay(waitTime);
  Serial.println("Switching RELAIS 7 OFF");
  digitalWrite(RELAIS_7, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 9 OFF");
  digitalWrite(RELAIS_9, LOW);
  delay(waitTime);
  Serial.println("Switching RELAIS 11 OFF");
  digitalWrite(RELAIS_11, LOW);
  delay(waitTime);
}

