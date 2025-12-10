$fn=100;

InnerWidth_x = 114;
InnerWidth_y = 70.5;
Inner_Height = 60;

ESPInnerWidth_x = 57;

Wall = 2;

OuterWidth_x = InnerWidth_x + 2*Wall;
OuterWidth_y = InnerWidth_y + 2*Wall;
OuterHeight = Inner_Height + Wall;

PCBLevel = 6;
PCBThickness = 1.5;
PCBOffset_x = 3;
PCBHolderOffset_x1 = 3.5;
PCBHolderOffset_x2 = 46;
PCBHolderOffset_y1 = 15.5;
PCBHolderOffset_y2 = 64;

StepDownHolderOffset_x1 = 60;
StepDownHolderOffset_x2 = 75;
StepDownHolderOffset_y1 = 22;
StepDownHolderOffset_y2 = 52;

CaseHolderWidth = 6;
CaseHolderHeight = 7;

ScrewDiameter = 2;
HolderWidth = 8;
HolderHeight = PCBLevel - PCBThickness;

ConnectorsWidth = 45;
ConnectorsHeight = 11;
ConnectorOffset_x = 3.5;

USBHoleWidth = 12;
USBHoleHeight = 7;
USBHoleXOffset = 20 + Wall + PCBOffset_x;
USBHoleZOffset = 14 + Wall + PCBLevel;

ACBladeRadius = 2;
ACConnectorZOffset = 45-19;
ACConnectorYOffset = 59;
ACConnectorHoleOffsetZ = 9.5;
ACConnectorHoleOffsetY = 7;
ACConnectorHoleDistanceY = 28.1;

VentingHolesDiameter = 4;
VentingHolesDistance = 6;
VentingHolesOffset = 6;


difference() {
    ESPCasePart();

    translate([OuterWidth_x, 0, OuterHeight+Wall+1])
        rotate([0, 180, 0]) 
            PowersupplyPart();
}

module ESPCasePart() {
    difference() {
        cube([OuterWidth_x, OuterWidth_y, OuterHeight]);
        translate([Wall, Wall, Wall])
            cube([InnerWidth_x, InnerWidth_y, Inner_Height+0.1]);
        translate([ESPInnerWidth_x + Wall, -0.1, Wall])
            cube([InnerWidth_x, OuterWidth_y+0.2, Inner_Height+0.1]);

        // Connectors
        translate([Wall+ConnectorOffset_x + PCBOffset_x, -2, PCBLevel+Wall]) 
            cube([ConnectorsWidth, 10, ConnectorsHeight]);

        // USB-C Port
        translate([0, InnerWidth_y, 0])
            USBHole();

        // Venting Holes
        VentingHoles();
    }
    CaseHolders(false);
    Holders();
}

module PowersupplyPart() {
      difference() {
        cube([OuterWidth_x, OuterWidth_y, OuterHeight]);
        translate([Wall, Wall, Wall])
            cube([InnerWidth_x, InnerWidth_y, Inner_Height+0.1]);
        translate([ESPInnerWidth_x + Wall, -0.1, Wall])
            cube([InnerWidth_x, OuterWidth_y+0.2, Inner_Height+0.1]);


    }

    CaseHolders(true);
}

module VentingHoles() {
    for (x = [0 : VentingHolesDistance : Inner_Height/2 - VentingHolesDistance]) {
        for (y = [0 : VentingHolesDistance : InnerWidth_y - VentingHolesDistance]) {
            translate([-1, Wall + VentingHolesOffset + y, x + Inner_Height/2])
                rotate([0, 90, 0])
                    cylinder(h=10, d=VentingHolesDiameter);
        }
    }

    for (x = [0 : VentingHolesDistance : Inner_Height/2 - VentingHolesDistance]) {
        for (y = [0 : VentingHolesDistance : ESPInnerWidth_x - VentingHolesDistance]) {
            translate([Wall + VentingHolesOffset + y, -1, x + Inner_Height/2])
                rotate([0, 90, 90])
                    cylinder(h=OuterWidth_y+10, d=VentingHolesDiameter);
        }
    }
}

module Holders() {    
    translate([Wall, Wall, Wall]) {
        // PCB Holders
        translate([PCBHolderOffset_x1+PCBOffset_x, PCBHolderOffset_y1, 0])
            Holder();
        translate([PCBHolderOffset_x2+PCBOffset_x, PCBHolderOffset_y1, 0])
            Holder();
        translate([PCBHolderOffset_x1+PCBOffset_x, PCBHolderOffset_y2, 0])
            Holder();
        translate([PCBHolderOffset_x2+PCBOffset_x, PCBHolderOffset_y2, 0])
            Holder();

        // Step-Down Holders
        translate([StepDownHolderOffset_x1, StepDownHolderOffset_y1, 0])
            Holder();
        translate([StepDownHolderOffset_x2, StepDownHolderOffset_y2, 0])
            Holder();            
    }
}

module Holder() {
    translate([-HolderWidth/2, -HolderWidth/2, 0])
        difference() {
            cube([HolderWidth, HolderWidth, HolderHeight]);
            translate([HolderWidth/2, HolderWidth/2, 0])
                cylinder(h=HolderHeight+1, d=ScrewDiameter);
        }
}

module CaseHolders(createHolepins) {
    translate([Wall, Wall+CaseHolderWidth, OuterHeight-CaseHolderHeight]) 
        rotate([0,0,-90])
            CaseHolder(CaseHolderWidth, CaseHolderWidth, CaseHolderHeight, createHolepins);
    translate([Wall, OuterWidth_y-Wall, OuterHeight-CaseHolderHeight]) 
        rotate([0,0,-90])
            CaseHolder(CaseHolderWidth, CaseHolderWidth, CaseHolderHeight, createHolepins);

    translate([Wall+ESPInnerWidth_x-CaseHolderWidth, Wall, OuterHeight-CaseHolderHeight]) 
        rotate([0,0,0])
            CaseHolder(CaseHolderWidth, CaseHolderWidth, CaseHolderHeight, createHolepins);
    translate([Wall+ESPInnerWidth_x, OuterWidth_y-Wall, OuterHeight-CaseHolderHeight]) 
        rotate([0,0,180])
            CaseHolder(CaseHolderWidth, CaseHolderWidth, CaseHolderHeight, createHolepins);       
}

module CaseHolder(widthX, widthY, height, createHolepins)
{
    difference()
    {
        cube([widthX, widthY, height]);
        rotate([-45,0,0])
            cube([widthX*2, widthY*2, height*2]);
        translate([widthX/2, widthY/2, 0])
            cylinder(h=100, d=ScrewDiameter);        
    }
            // rotate([-45,0,0])
            // cube([widthX*2, widthY*2, height*2]);

    if (createHolepins)
        translate([widthX/2, widthY/2, 0])
            cylinder(h=100, d=ScrewDiameter+1); 
}

module USBHole()
{
    translate([USBHoleXOffset+USBHoleHeight/2, 10, USBHoleZOffset])
    {
        hull()
        {
            rotate([90, 0, 0])
                cylinder(h=20, d=USBHoleHeight);
            translate([USBHoleWidth-USBHoleHeight, 0, 0])
                rotate([90, 0, 0])
                    cylinder(h=20, d=USBHoleHeight);
        }
    }
}

module ACConnector()
{
    p1 = [0, 0];
    p2 = [27, 0];
    p3 = [27, 14];
    p4 = [22, 19];
    p5 = [5, 19];
    p6 = [0, 14];

    translate([-5, ACConnectorYOffset, ACConnectorZOffset])
        rotate([90,0,90])
            linear_extrude(height=10)
                polygon([p1, p2, p3, p4, p5, p6]);
}