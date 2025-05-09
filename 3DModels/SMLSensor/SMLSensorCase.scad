$fn = 100;

Magnet_Outer_Diameter = 27.0;
Magnet_Inner_Diameter = 16.0;
Magnet_Height = 5.0;

Border_Bottom = 1.0;
Border_Side = 2.0;

LED_Diameter = 5.0;
LED_Full_Distance = 12.0;
LED_Height = 8.0;
LED_Z_Offset = LED_Height - Magnet_Height;
StatusLED_Diameter = 5; // This is the LED in the cap

Corner_Radius = 2.0;
InnerWidth_X = 25.0;
InnerWidth_Y = 25.0;
InnerHeight = 19.0;

USBPort_Width = 14.0;
USBPort_Height = 7.0;
USBPort_Offset_Y = 11.5;
USBPort_Offset_Z = 12.5;

//MagnetRing();
ModuleBox();
//Cap();

module Cap()
{
    translate([0, 0, 0])
    {
        difference()
        {
            roundrect([InnerWidth_X + Border_Side, InnerWidth_Y + Border_Side], Corner_Radius, height=Border_Bottom);
            cylinder(h=100, d=StatusLED_Diameter, center = true);
        }
    }
    translate([0, 0, 2*Border_Bottom])
    {
        difference()
        {
            cube([InnerWidth_X, InnerWidth_Y, 3*Border_Bottom], center = true);
            cylinder(h=100, d=6, center = true);
        }
    }
}

module MagnetRing()
{
    translate([30, 0, 0])
    {
        difference()
        {
            cylinder(h = Magnet_Height + Border_Bottom +  LED_Z_Offset, d = Magnet_Outer_Diameter + 2*Border_Side);
            translate([0, 0, Border_Bottom])
            {
                Magnet(Magnet_Height + LED_Z_Offset);
            }            
            cylinder(h = Magnet_Height + Border_Bottom +  LED_Z_Offset, d = Magnet_Inner_Diameter - Border_Side);
        }
    }
}

module ModuleBox()
{
    translate([0, 0, Magnet_Height + Border_Bottom + LED_Z_Offset])
    {
        Box();
        translate([0, 0, -LED_Z_Offset])
        {
            Magnet(LED_Z_Offset);
        }
    }
}

module LEDHole(direction)
{
    translate([0, (LED_Full_Distance-LED_Diameter)/2*direction, 0])
    {
        cylinder(h = 10.0, d = LED_Diameter);
    }
}

module Magnet(height)
{
    difference()
    {
        cylinder(h = height, d = Magnet_Outer_Diameter);
        translate([0, 0, 0])
        {
            cylinder(h = height, d = Magnet_Inner_Diameter);
        }
    }
}

module Box()
{
    difference()
    {
        translate([0, 0, 0])
        {
            roundrect([InnerWidth_X + Border_Side, InnerWidth_Y + Border_Side], Corner_Radius, height=InnerHeight + 2*Border_Bottom);
        }
        translate([0, 0, Magnet_Height + LED_Z_Offset + 3*Border_Bottom])
        {
            cube([InnerWidth_X, InnerWidth_Y, InnerHeight + 2*Border_Bottom], center = true);
        }

        LEDHole(1);
        LEDHole(-1);
        USBHole();
    }
}

module USBHole()
{
    translate([0, 2.5-USBPort_Offset_Y, USBPort_Offset_Z])
    {
        cube([20, USBPort_Width, USBPort_Height]);
    }
}

module roundrect(size, radius, height)
{
    hull()
    {
        for (i = [0:3])
        {
            x = size.x * (i % 2 - 0.5);
            y = size.y * (floor(i/2) - 0.5);
            translate([x, y, 0])
            {
                cylinder(r = radius, h = height);
            }
        }
    }
}
 