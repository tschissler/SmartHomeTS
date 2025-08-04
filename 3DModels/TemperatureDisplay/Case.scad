$fn=100;

InnerDepth = 100;
InnerHeight = 18;
InnerWidth = 167;
InnerGap = 2;

WallThickness = 1.6;

ScrewHoleDiameter = 3.0;
ScrewHeadDiameter = 7.0;
ScrewHeadHeight = 3.0;

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

difference()
{
Case();
Holes();
}

module Case() {
    difference() {
        // Outer shell
        cube([OuterWidth, OuterDepth, OuterHeight]);
        
        // Inner cavity
        translate([WallThickness, WallThickness, WallThickness])
            cube([InnerWidth, InnerDepth, InnerHeight+10]);
    }
    ScrewExtensions();
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