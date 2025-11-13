$fn=100;

Case_Width_X = 180;
Case_Width_Y = 80;
Case_Wall = 2.4;
Case_Wall_Longside = 2;
Case_Height = 75;
Case_Width_Y2 = 75;

FrontHolderWidth = 10;

UsbConnectorHeigh = 8;
UsbConnectorWidth = 14;
AudioConnectorWidth = 27.5;
AudioConnectorHeight = 8.5;

Display_Width_X = 97;
Display_Width_Y = 40;
Display_Height = 11.5;
DisplayHolder_Width = 12;
DisplayHolder_Offset_X = 3;
DisplayHolder_Offset_Y = 3;
DisplayHolder_ScrewDiameter = 3;
DisplayHolder_ScrewDiameter_Outer = 4.5;
DisplayPCB_Width_X = 99;
DisplayPCB_Width_Y = 60.5;
DisplayPCB_Height = 1.5;

AmpKnob_Diameter = 7;
AmpKnob_Diameter_Outer = 30;
AmpKnob_Height = 12;

Joystick_Diameter = 20;
Joystick_Diameter2 = 27;
JoystickHolder_Height = 18.5;
JoystickHolder_Width = 7.5;
JoystickHolder_Offset_Left = 11.5;
JoystickHolder_Offset_Right = 8.5;
JoystickHolder_Offset_Top = 16;
JoystickHolder_Offset_Bottom = 10.5;

ESPHolder_Distance = 55;
ESPHolder_Heigh = 5;
ESPHolder_ThreadDiameter = 4;

difference()
{
    Case();
    translate([0,6,-1])
        Front();
}
//ESPHolder();

//AmpKnob();

module AmpKnob()
{
    difference()
    {
        RoundedCylinder(AmpKnob_Height + Case_Wall, AmpKnob_Diameter_Outer, 3);
        cylinder(h=AmpKnob_Height, d=AmpKnob_Diameter-1);
        cylinder(h=5, d1=AmpKnob_Diameter+5, d2=AmpKnob_Diameter-1);
    }
}

module RoundedCylinder(height, diameter, radius)
{
    hull()
    {
        translate([0,0,radius])
            rotate_extrude(convexity = 10)
                translate([diameter/2-radius, 0, 0])
                    circle(r = radius);
        translate([0,0,height - radius])
            rotate_extrude(convexity = 10)
                translate([diameter/2-radius, 0, 0])
                    circle(r = radius);
    }
}

module Case()
{
    difference()
    {
        translate([0, 46, -44])
            rotate([45, 0, 0])
                CaseBase();

        translate([0, 0, -201])
            cube([200, 200, 200]);

        translate([Case_Width_X-5, Case_Width_X/6, Case_Width_X/5])
            UsbHole();    
        translate([Case_Width_X/1.5, Case_Width_X/3-10, Case_Width_X/3-10.9])
            AudioConnectorHole();

        FrontHolderHoles();
        translate([0, Case_Width_Y-DisplayHolder_ScrewDiameter, 0])
            FrontHolderHoles();
    }
}

module FrontHolderHoles()
{
    translate([Case_Wall + FrontHolderWidth/2, FrontHolderWidth-Case_Wall, -20])
        cylinder(h=35, d=DisplayHolder_ScrewDiameter);
    translate([Case_Width_X/2, FrontHolderWidth-Case_Wall, -20])
        cylinder(h=35, d=DisplayHolder_ScrewDiameter);
    translate([Case_Width_X-FrontHolderWidth/2-Case_Wall, FrontHolderWidth-Case_Wall, -20])
        cylinder(h=35, d=DisplayHolder_ScrewDiameter);
}

module CaseBase()
{
    difference()
    {
        cube([Case_Width_X, Case_Width_Y2, Case_Height]);
        translate([Case_Wall, Case_Wall_Longside, Case_Wall_Longside])
            cube([Case_Width_X-2*Case_Wall, Case_Width_Y2-2*Case_Wall_Longside, Case_Height-2*Case_Wall_Longside]);
    }

    translate([Case_Wall, 0, Case_Wall])
        cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);
    translate([(Case_Width_X-FrontHolderWidth)/2, 0, Case_Wall])
        cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);
    translate([Case_Width_X-FrontHolderWidth-Case_Wall, 0, Case_Wall])
        cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);

    rotate([90, 0, 0])
    {
        translate([Case_Wall, 0, -FrontHolderWidth-Case_Wall])
            cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);
        translate([(Case_Width_X-FrontHolderWidth)/2, 0, -FrontHolderWidth-Case_Wall])
            cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);
        translate([Case_Width_X-FrontHolderWidth-Case_Wall, 0, -FrontHolderWidth-Case_Wall])
            cube([FrontHolderWidth, Case_Width_Y2, FrontHolderWidth]);
    }
}

module AudioConnectorHole()
{
    rotate([45,0,0])
        cube([AudioConnectorWidth, AudioConnectorHeight, 10]);
}

module UsbHole()
{
    rotate([45,0,0])
        rotate([0,90,0])
            hull()
            {
                cylinder(h=10, d=UsbConnectorHeigh);
                translate([0, UsbConnectorWidth-UsbConnectorHeigh, 0])
                    cylinder(h=10, d=UsbConnectorHeigh);
            }
}

module ESPHolder()
{
    ESPHolderWidth = DisplayPCB_Width_X + DisplayHolder_Offset_X*2;
    translate([(Case_Width_X-ESPHolderWidth)/2, (Case_Width_Y-DisplayPCB_Width_Y)-DisplayHolder_Width-0.75, Display_Height])
        difference()
        {
            union()
            {
                cube([ESPHolderWidth, DisplayHolder_Width, Case_Wall*2]);
                translate([(ESPHolderWidth-ESPHolder_Distance-DisplayHolder_Width)/2 ,0,Case_Wall*2])
                    cube([DisplayHolder_Width, DisplayHolder_Width, ESPHolder_Heigh]);
                translate([(ESPHolderWidth-ESPHolder_Distance-DisplayHolder_Width)/2+ESPHolder_Distance ,0,Case_Wall*2])
                    cube([DisplayHolder_Width, DisplayHolder_Width, ESPHolder_Heigh]);
            }

            // Screwholes
            translate([DisplayHolder_Width/2, DisplayHolder_Width/2,0])
                cylinder(h=200, d=DisplayHolder_ScrewDiameter_Outer);
            translate([ESPHolderWidth-DisplayHolder_Width/2, DisplayHolder_Width/2,0])
                cylinder(h=200, d=DisplayHolder_ScrewDiameter_Outer);

            // Threads
            translate([(ESPHolderWidth-ESPHolder_Distance)/2 , DisplayHolder_Width/2, 0])
                cylinder(h=200, d=ESPHolder_ThreadDiameter);
            translate([(ESPHolderWidth-ESPHolder_Distance)/2+ESPHolder_Distance , DisplayHolder_Width/2, 0])
                cylinder(h=200, d=ESPHolder_ThreadDiameter);
        }
}

module Front()
{
    difference()
    {
        union()
        {
            cube ([Case_Width_X, Case_Width_Y, Case_Wall]);
            DisplayHolders();
            JoystickHolders();
        }
        translate([(Case_Width_X-Display_Width_X)/2, (Case_Width_Y-Display_Width_Y)/2,-1])
            cube([Display_Width_X, Display_Width_Y, 10]);
        translate([Case_Width_X-(Case_Width_X-Display_Width_X)/4, Case_Width_Y/2, -1])
            cylinder(h=10, d=AmpKnob_Diameter);
        translate([(Case_Width_X-Display_Width_X)/4, Case_Width_Y/2, -1])
            cylinder(h=100, d=Joystick_Diameter);
        translate([(Case_Width_X-Display_Width_X)/4, Case_Width_Y/2, 0.5])
            cylinder(h=100, d=Joystick_Diameter2);
    }


}

module JoystickHolders()
{
    JoystickHolder_Left = (Case_Width_X-Display_Width_X)/4 - JoystickHolder_Offset_Left;
    JoystickHolder_Right = (Case_Width_X-Display_Width_X)/4 + JoystickHolder_Offset_Right;
    JoystickHolder_Top = Case_Width_Y/2 + JoystickHolder_Offset_Top;
    JoystickHolder_Bottom = Case_Width_Y/2 - JoystickHolder_Offset_Bottom;

    translate([JoystickHolder_Left, JoystickHolder_Top, 0])
        ScrewHolder(JoystickHolder_Width, JoystickHolder_Height, DisplayHolder_ScrewDiameter);
    translate([JoystickHolder_Right, JoystickHolder_Top, 0])
        ScrewHolder(JoystickHolder_Width, JoystickHolder_Height, DisplayHolder_ScrewDiameter);
    translate([JoystickHolder_Left, JoystickHolder_Bottom, 0])
        ScrewHolder(JoystickHolder_Width, JoystickHolder_Height, DisplayHolder_ScrewDiameter);
    translate([JoystickHolder_Right, JoystickHolder_Bottom, 0])
        ScrewHolder(JoystickHolder_Width, JoystickHolder_Height, DisplayHolder_ScrewDiameter);
}

module DisplayHolders()
{
    displayHolder_Left = (Case_Width_X-DisplayPCB_Width_X)/2+DisplayHolder_Offset_X;
    displayHolder_Right = Case_Width_X-(Case_Width_X-DisplayPCB_Width_X)/2-DisplayHolder_Offset_X;
    displayHolder_Bottom = (Case_Width_Y-DisplayPCB_Width_Y)/2+DisplayHolder_Offset_Y;
    displayHolder_Top = Case_Width_Y-(Case_Width_Y-DisplayPCB_Width_Y)/2-DisplayHolder_Offset_Y;

    difference()
    {
        union()
        {
            translate([displayHolder_Left, displayHolder_Bottom, 0])
                ScrewHolder(DisplayHolder_Width, Display_Height, DisplayHolder_ScrewDiameter);
            translate([displayHolder_Right, displayHolder_Bottom, 0])
                ScrewHolder(DisplayHolder_Width, Display_Height, DisplayHolder_ScrewDiameter);
            translate([displayHolder_Left, displayHolder_Top, 0])
                ScrewHolder(DisplayHolder_Width, Display_Height, DisplayHolder_ScrewDiameter);
            translate([displayHolder_Right, displayHolder_Top, 0])
                ScrewHolder(DisplayHolder_Width, Display_Height, DisplayHolder_ScrewDiameter);
        }
        translate([(Case_Width_X-DisplayPCB_Width_X)/2, (Case_Width_Y-DisplayPCB_Width_Y)/2,Display_Height-DisplayPCB_Height])
            cube([DisplayPCB_Width_X, DisplayPCB_Width_Y, 10]);
    }
}

module ScrewHolder(width, height, diameter)
{
    difference()
    {
        translate([-width/2, -width/2, 0])
            cube([width, width, height]);
        cylinder(d=diameter, h=height+1);
    }
}