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

Sensor_Width = 14.8;
Sensor_Height = 8;
Sensor_Length = 7;

LED_Length = 16;
LED_Width = 7.5;

wall = 2;
connector_Diameter = 2.5;
connector_Height = 6;

Full_Length = USB_length + Board_Length + Sensor_Length ;
Full_Heigh = BoardHeight + 2 * wall;
Full_Width = BoardWidth + 2 * wall;

upper = 1;

difference()
{
    cube([BoardWidth+2*wall, Full_Length, Full_Heigh]);
    translate([(Full_Width- USB_width)/2, 0 , (Full_Heigh-USB_height)/2])
        cube([USB_width, USB_length, USB_height]);
    translate([(Full_Width-BoardWidth)/2, USB_length , (Full_Heigh-BoardHeight)/2])
        cube([BoardWidth, Board_Length, BoardHeight]);
    translate([(Full_Width-Sensor_Width)/2, USB_length+Board_Length , (Full_Heigh-Sensor_Height)/2])
        cube([Sensor_Width, Sensor_Length, Sensor_Height]);
    // USB-C
    translate([(Full_Width-wall)*USBC_Side, USB_length+(Board_Length-USBC_Width)/2 , (Full_Heigh-USBC_Height)/2])
        cube([5, USBC_Width, USBC_Height]);

    // LED
    translate([(Full_Width-LED_Width)/2, USB_length+(Board_Length-LED_Length)/2 , 0.5])
        cube([LED_Width, LED_Length, LED_Width]);

    translate([-1,-1,Full_Heigh/2*upper])
        cube([BoardWidth+2*wall+2, Full_Length+2, Full_Heigh/2+.001]);

    translate([(Full_Width-Sensor_Width)/4, Full_Length-Sensor_Length/2, (Full_Heigh-connector_Height)/2-2])
        cylinder(d=connector_Diameter, h=connector_Height+2);
    translate([Full_Width-(Full_Width-USB_width)/4, USB_length/2, (Full_Heigh-connector_Height)/2-2])
        cylinder(d=connector_Diameter, h=connector_Height+2);
}



translate([(Full_Width-USB_width)/4, USB_length/2, (Full_Heigh-connector_Height)/2])
    cylinder(d=connector_Diameter, h=connector_Height);
translate([Full_Width-(Full_Width-Sensor_Width)/4, Full_Length-Sensor_Length/2, (Full_Heigh-connector_Height)/2])
    cylinder(d=connector_Diameter, h=connector_Height);
