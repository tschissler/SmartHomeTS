$fn=100;

InnerDepth = 100;
InnerHeight = 18;
InnerWidth = 167;
InnerGap = 2;

WallThickness = 1.6;

ScrewHoleDiameter = 3.0;
ScrewHeadDiameter = 7.0;
ScrewHeadHeight = 4.0;

ScrewExtensionHeight = 1.5;
ScrewExtensionWidth = 15;
ScrewExtensionDepth = 15;
ScrewOffsetX = 20;
ScrewOffsetYBottom = 18.5 + InnerGap;
ScrewOffsetYTop = 14;

OuterDepth = InnerDepth + WallThickness * 2;
OuterHeight = InnerHeight + WallThickness;
OuterWidth = InnerWidth + WallThickness * 2;

ScrewOffsetX1 = ScrewOffsetX + WallThickness;
ScrewOffsetX2 = OuterWidth - ScrewOffsetX - WallThickness;
ScrewOffsetY1 = ScrewOffsetYBottom + WallThickness;
ScrewOffsetY2 = OuterDepth - ScrewOffsetYTop - WallThickness;

CableGapWidth = 7.5;
CableGapDepth = 12;
CableGapOffset = OuterHeight - CableGapDepth;
CableGapY = 45;

DoveTailWidth1 = 15;
DoveTailWidth2 = 10;
DoveTailHeight = 6;
DoveTailBorder = 3;
DoveTailBase = 20;
DoveTailOffset = 48;
DoveTailLength = InnerDepth - 20;


ButtonHoleWidth = 6.0;
ButtonHoleHeight = 3.0;
ButtonHoleOffsetFromBottom = 6.5;
Button1OffsetFromLeft = 73.5;
Button2OffsetFromLeft = 84.5;

Case();
Holder();

module Holder() {
    translate([0,0, -WallThickness])
        cube([OuterWidth, OuterDepth, WallThickness]);

    DoveTail(0.5);
}

module Case()
{
    difference()
    {
        CaseMain();
        Holes();
        DoveTail();    
    }
}

module DoveTail(gap=0) {
    translate([DoveTailWidth1 + DoveTailOffset, DoveTailLength, DoveTailHeight])
        rotate([90, 180, 0]) {
            DoveTailTrail(gap);            
        }
    translate([OuterWidth - DoveTailOffset, DoveTailLength, DoveTailHeight])
        rotate([90, 180, 0]) {
            DoveTailTrail(gap);            
        }
}

module DoveTail1() {
    difference() {
        translate([-DoveTailBorder, 0, 0])
            cube([DoveTailWidth1+2*DoveTailBorder, DoveTailHeight, DoveTailBase]);

        DoveTailTrail();
    }
    translate([-DoveTailBorder, -WallThickness, 0])
        cube([DoveTailWidth1+2*DoveTailBorder, WallThickness, DoveTailBase]);
}

module DoveTail2() {
    translate([-DoveTailBorder, DoveTailHeight, 0])
        cube([DoveTailWidth1+2*DoveTailBorder, WallThickness, DoveTailBase]);
    DoveTailTrail(0.2);
}

module DoveTailTrail(gap=0) {
    widthDifference = (DoveTailWidth1 - DoveTailWidth2) / 2 + gap;
    p1 = [gap, 0];
    p2 = [DoveTailWidth1-gap, 0];
    p3 = [DoveTailWidth1 - widthDifference, DoveTailHeight];
    p4 = [widthDifference, DoveTailHeight];

    translate([0, 0, 0])
        linear_extrude(height=DoveTailLength)
            polygon(points=[p1, p2, p3, p4], paths=[[0, 1, 2, 3]]);    
}

module CaseMain() {
    difference() {
        // Outer shell
        cube([OuterWidth, OuterDepth, OuterHeight]);
        
        // Inner cavity
        translate([WallThickness, WallThickness, WallThickness])
            cube([InnerWidth, InnerDepth, InnerHeight+10]);
    }
    ScrewExtensions();
    DoveTailExtensions();
}

module Holes() {
    translate([ScrewOffsetX1, ScrewOffsetY1, 0])
    {
        cylinder(h=OuterHeight, d=ScrewHoleDiameter, center=true);
        cylinder(h=ScrewHeadHeight, d=ScrewHeadDiameter, center=true);
    }
    translate([ScrewOffsetX1, ScrewOffsetY2, 0])
    {
        cylinder(h=OuterHeight, d=ScrewHoleDiameter, center=true);
        cylinder(h=ScrewHeadHeight, d=ScrewHeadDiameter, center=true);
    }
    translate([ScrewOffsetX2, ScrewOffsetY1, 0])
    {
        cylinder(h=OuterHeight, d=ScrewHoleDiameter, center=true);
        cylinder(h=ScrewHeadHeight, d=ScrewHeadDiameter, center=true);
    }
    translate([ScrewOffsetX2, ScrewOffsetY2, 0])
    {
        cylinder(h=OuterHeight, d=ScrewHoleDiameter, center=true);
        cylinder(h=ScrewHeadHeight, d=ScrewHeadDiameter, center=true);
    }
    CableGap();
    ButtonHole(Button1OffsetFromLeft);
    ButtonHole(Button2OffsetFromLeft);
}

module ButtonHole(offset) {
    translate([offset, InnerDepth, ButtonHoleOffsetFromBottom])
        cube([ButtonHoleWidth, 20, ButtonHoleHeight]);
}

module CableGap() {
    translate([0, CableGapY + WallThickness, CableGapOffset])
        cube([10, CableGapWidth, OuterHeight - CableGapOffset]);
}

module ScrewExtensions() {
    translate([ScrewOffsetX1-ScrewExtensionWidth/2, ScrewOffsetY1-ScrewExtensionWidth/2, WallThickness])
        cube([ScrewExtensionWidth, ScrewExtensionDepth, ScrewExtensionHeight]);
    translate([ScrewOffsetX1-ScrewExtensionWidth/2, ScrewOffsetY2-ScrewExtensionWidth/2, WallThickness])
        cube([ScrewExtensionWidth, ScrewExtensionDepth, ScrewExtensionHeight]);
    translate([ScrewOffsetX2-ScrewExtensionWidth/2, ScrewOffsetY1-ScrewExtensionWidth/2, WallThickness])
        cube([ScrewExtensionWidth, ScrewExtensionDepth, ScrewExtensionHeight]);
    translate([ScrewOffsetX2-ScrewExtensionWidth/2, ScrewOffsetY2-ScrewExtensionWidth/2, WallThickness])
        cube([ScrewExtensionWidth, ScrewExtensionDepth, ScrewExtensionHeight]);
}

module DoveTailExtensions() {
    translate([DoveTailOffset-WallThickness, 0, 0])
        cube([DoveTailWidth1 + 2 * WallThickness, DoveTailLength, DoveTailHeight + WallThickness]);
    translate([OuterWidth-DoveTailOffset-DoveTailWidth1-WallThickness, 0, 0])
        cube([DoveTailWidth1 + 2 * WallThickness, DoveTailLength, DoveTailHeight + WallThickness]);
}