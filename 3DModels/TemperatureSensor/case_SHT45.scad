$fn=40;

USB_width = 11.8;
USB_height = 4.8;
USB_length = 16;

USBC_Height = 3.5;
USBC_Width = 9;
USBC_Side = 1;

BoardWidth = 21;
BoardHeight = 9;
Board_Length = 25;

Sensor_Width = 17.8;
Sensor_Height = 9;
Sensor_Length = 28;
Sensor_Wall = 2;

LED_Length = 16;
LED_Width = 7.5;

wall = 2;
connector_Diameter = 2.5;
connector_Height = 6;

Full_Length = USB_length + Board_Length + Sensor_Length + Sensor_Wall;
Full_Heigh = BoardHeight + 2 * wall;
Full_Width = BoardWidth + 2 * wall;

Grid_Size = 2.9;
Grid_Gap = 0.8;

upper = 0;

difference()
{
    cube([BoardWidth+2*wall, Full_Length, Full_Heigh]);
    // USB-A
    translate([(Full_Width-USB_width)/2, -1 , (Full_Heigh-USB_height)/2])
        cube([USB_width, USB_length, USB_height]);
    translate([(Full_Width-USB_width/2)/2, 0 , (Full_Heigh-USB_height)/2])
        cube([USB_width/2, USB_length, USB_height]);
    // Board
    translate([(Full_Width-BoardWidth)/2, USB_length , (Full_Heigh-BoardHeight)/2])
        cube([BoardWidth, Board_Length, BoardHeight]);
    // Sensor
    translate([(Full_Width-Sensor_Width)/2, USB_length+Board_Length-Sensor_Wall , (Full_Heigh-Sensor_Height)/2])
        cube([Sensor_Width, Sensor_Length, Sensor_Height]);
    // USB-C
    translate([(Full_Width-wall)*USBC_Side, USB_length+(Board_Length-USBC_Width)/2 , (Full_Heigh-USBC_Height)/2])
        cube([5, USBC_Width, USBC_Height]);
    // LED
    translate([(Full_Width-LED_Width)/2, USB_length+(Board_Length-LED_Length)/2 , 0.5])
        cube([LED_Width, LED_Length, LED_Width]);

    translate([-1,-1,Full_Heigh/2*upper])
        cube([BoardWidth+2*wall+2, Full_Length+2, Full_Heigh/2+.001]);

    if (upper == 1) {
        Pins1();
    }
    else {
        Pins2();
    }
    Grid();
}

if (upper == 1) {
    Pins2();
}
else {
    Pins1();
}

module Pins1()
{
    translate([3, Full_Length-4, (Full_Heigh-connector_Height)/2-2*upper])
        cylinder(d=connector_Diameter, h=connector_Height+2);
    translate([Full_Width-(Full_Width-USB_width)/4, USB_length/2, (Full_Heigh-connector_Height)/2-2*upper])
        cylinder(d=connector_Diameter, h=connector_Height+2);
}

module Pins2()
{
    height = (Full_Heigh-connector_Height)/2-2*upper;
    translate([(Full_Width-USB_width)/4, USB_length/2, height])
        cylinder(d=connector_Diameter, h=connector_Height+2);
    translate([Full_Width-3, Full_Length-4, height])
        cylinder(d=connector_Diameter, h=connector_Height+2);
}

module Grid()
{
    translate([(Full_Width-Sensor_Width)/2, Full_Length-Grid_Gap-Grid_Size-Sensor_Wall-Grid_Gap*2, -0.1])
    for (y=[0:6]) {
        for (x=[0:4]) {
            translate([x*(Grid_Size+Grid_Gap), -y*(Grid_Size+Grid_Gap), 0])
                cube([Grid_Size, Grid_Size, 40]);
        }
    }
        
}