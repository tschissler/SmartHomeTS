# ESP32 based CAN-Gateway for Hoval heat pump

## Communication details
The CAN-Bus communicates with 50KBaud

A CAN-Bus frame start with 0xAA and ends with 0x55

Example-frame:
| Byte  | Example | Description     |
| :---: | :-----: | --------------- |
|   0   |   AA    | Start           |
|   1   |   E8    | Header          |
|   2   |   FF    | Header          |
|   3   |   0F    | Header          |
|   4   |   C0    | Header          |
|   5   |   1F    | Header          |
|
|   6   |   01    | Unit ID         |
|   7   |   42    | ANSWER          |
|   8   |   00    | Function Group  |
|   9   |   00    | Function Number |
|  10   |   00    | DataPoint High  |
|  11   |   00    | DataPoint Low   |
|  12   |   00    | Data High       |
|  13   |   CD    | Data Low        |
|  14   |   55    | End             |



Header
|  1-8 |  9-16  | 17-24 | 25-32| 33-40 |
| :---: | :---: | :---: | :---: | :---: |
|   E8      |     FF    |   0F      |   C0      |   1F  (1)  |
| 1110 1000 | 1111 1111 | 0000 1111 | 1100 0000 | 0001 1111 |



| Start | Identifier A | SRR | IDE | Identifier B | RTR | R1 | R0 | DataLength |
| :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| 1b | 11b          | 1b | 1b | 18b | 1b | 1b | 1b | 4b |
| 1 | 110 1000 1111 | 1  | 1 | 11 0000 1111 1100 0000  | 0 | 0 | 0 | 1111 |1  |
