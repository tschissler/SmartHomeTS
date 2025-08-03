$fn=100;

InnerDepth = 100;
InnerHeight = 18;
InnerWidth = 157;

WallThickness = 2;

ScrewHoleDiameter = 3.0;
ScrewHeadDiameter = 7.0;
ScrewHeadHeight = 2.0;

ScrewExtensionHeight = 1.5;
ScrewExtensionWidth = 15;
ScrewExtensionDepth = 15;
ScrewOffsetX = 20;
ScrewOffsetY = 18.2;

OuterDepth = InnerDepth + WallThickness * 2;
OuterHeight = InnerHeight + WallThickness * 2;
OuterWidth = InnerWidth + WallThickness * 2;

ScrewOffsetX1 = ScrewOffsetX + WallThickness;
ScrewOffsetX2 = OuterWidth - ScrewOffsetX - WallThickness;
ScrewOffsetY1 = ScrewOffsetY + WallThickness;
ScrewOffsetY2 = OuterDepth - ScrewOffsetY + WallThickness;

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