$fn = 100;

BoxWidthX = 174;
BoxWidthY = 105;
BoxRadius = 2;
BoxHeightInner = 50;
BoxBottom = 2;
BoxHeightOuter = BoxHeightInner + 2 *BoxBottom;
BoxBottomHeight = 14;

InsertHeight = 7;
InsertDiameter = 4;
InsertHolderWidth = 10;
InsertOffsetX1 = 20;
InsertOffsetX2 = InsertOffsetX1 + 42.5;
InsertOffsetX3 = InsertOffsetX2 + 100;
InsertOffsetY1 = 27.5;
InsertOffsetY2 = InsertOffsetY1 + 50;

ACBladeRadius = 2;
ACConnectorZOffset = 45-19;
ACConnectorYOffset = 59;
ACConnectorHoleOffsetZ = 9.5;
ACConnectorHoleOffsetY = 7;
ACConnectorHoleDistanceY = 28.1;

CableWidth = 6;
CableHeight = 4;
CableHolderXOffset = 20;
CableHolderZOffset = 40;
CableHolderHeight = 15;
CableHolderWidthX = BoxWidthX-CableHolderXOffset*2;
CableHolderWidthY = 10;
CableHoleNumber = 8;
CableHolderTopHeight = 3;
CableHolderTopGap = 1.5;

SideMountWidthX = 10;
SideMountWidthY = 12;

USBHoleWidth = 12;
USBHoleHeight = 7;
USBHoleXOffset = 20;
USBHoleZOffset = 22;

ScrewDiameterInner = 2.5;
ScrewDiameterOuter = 3.5;

LEDDiameter = 5;
LEDDistance = 14.85;
LEDOffsetX = 35;
LEDOffsetY = 30;
LEDHolderWidthX = 10;
LEDHolderWidthY = 25;
LEDHolderHeight = 5;
LEDHolderOffsetX1 = LEDOffsetX-27-LEDHolderWidthX/2;
LEDHolderOffsetX2 = LEDHolderOffsetX1 + 76;
LEDHolderOffsetX3 = LEDHolderOffsetX2 + 4.5;
LEDHolderOffsetX4 = LEDHolderOffsetX3 + 76;



BottomPart();
//InsertHolders();
//  translate([0, 150, 0])
//      TopPart();

//CableHolderTop();

module CableHolderTop()
{
    difference()
    {
        translate([CableHolderXOffset,0, CableHolderZOffset + CableHolderTopGap])
            cube([CableHolderWidthX,CableHolderWidthY,CableHolderTopHeight]);

        // Cable gaps
        for (i=[1:CableHoleNumber]) {
            translate([CableHolderXOffset + (i-0.5)*CableHolderWidthX/(CableHoleNumber+1)+8.5, 0, CableHolderZOffset])
                CableHole();
        }
        // Screw holes
        for (i=[1:CableHoleNumber+1]) {
            translate([CableHolderXOffset + (i-1)*CableHolderWidthX/(CableHoleNumber+1)+7.5, CableHolderWidthY/2, 0])
                cylinder(h=100, d=ScrewDiameterOuter);  
        }
    }
}

module BottomPart(createHoles=true)
{
    difference()
    {
        ACBlade();
        if (createHoles)
            ACConnector();
    }
    difference()
    {
        union()
        {
            difference()
            {
                Box();
                translate([-10, 2, BoxBottomHeight])
                    cube([200, 200, 200]);
                translate([1, 1, BoxBottomHeight-2])
                    cube([BoxWidthX, BoxWidthY, 200]);
            }
            CableHolder();
        }
        if (createHoles)
        {
            // Cable gaps
            for (i=[1:CableHoleNumber]) {
                translate([CableHolderXOffset + (i-0.5)*CableHolderWidthX/(CableHoleNumber+1)+8.5, 0, CableHolderZOffset])
                    CableHole();
            }
            // Screw holes
            for (i=[1:CableHoleNumber+1]) {
                translate([CableHolderXOffset + (i-1)*CableHolderWidthX/(CableHoleNumber+1)+7.5, CableHolderWidthY/2, 0])
                    cylinder(h=100, d=ScrewDiameterInner);  
            }
        }
        if (createHoles)
            USBHole();
    }
    SideMounts(!createHoles);
    BottomMount(!createHoles);
}

module TopPart()
{
    difference()
    {
        Box();
        BottomPart(false);
        LEDHoles();
    }
    LEDHolders();
}

module LEDHoles()
{
    for (i=[1:CableHoleNumber]) {
        translate([LEDOffsetX + (i-1)*LEDDistance, LEDOffsetY, 0])
            cylinder(h=100, d=LEDDiameter);  
    }
}

module LEDHolders()
{
    difference()
    {
        union()
        {
            translate([LEDHolderOffsetX1, LEDOffsetY-LEDHolderWidthY/2, BoxHeightInner-LEDHolderHeight])
                cube([LEDHolderWidthX, LEDHolderWidthY, LEDHolderHeight]);
            translate([LEDHolderOffsetX2, LEDOffsetY-LEDHolderWidthY/2, BoxHeightInner-LEDHolderHeight])
                cube([LEDHolderWidthX, LEDHolderWidthY, LEDHolderHeight]);
            translate([LEDHolderOffsetX3, LEDOffsetY-LEDHolderWidthY/2, BoxHeightInner-LEDHolderHeight])
                cube([LEDHolderWidthX, LEDHolderWidthY, LEDHolderHeight]);
            translate([LEDHolderOffsetX4, LEDOffsetY-LEDHolderWidthY/2, BoxHeightInner-LEDHolderHeight])
                cube([LEDHolderWidthX, LEDHolderWidthY, LEDHolderHeight]);
        }
        translate([LEDHolderOffsetX1, LEDOffsetY-(LEDHolderWidthY/2-9), BoxHeightInner-LEDHolderHeight])
                cube([200, 7, LEDHolderHeight]);
    }
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

module ACBlade() 
{
    difference()
    {
        hull()
        {
            translate([-BoxRadius, ACBladeRadius, ACBladeRadius])
                rotate([0, 90, 0])
                    cylinder(h=BoxRadius, r=ACBladeRadius);
            translate([-BoxRadius, BoxWidthY-ACBladeRadius, ACBladeRadius])
                rotate([0, 90, 0])
                    cylinder(h=BoxRadius, r=ACBladeRadius);
            translate([-BoxRadius, ACBladeRadius, BoxHeightInner-ACBladeRadius])
                rotate([0, 90, 0])
                    cylinder(h=BoxRadius, r=ACBladeRadius);
            translate([-BoxRadius, BoxWidthY-ACBladeRadius, BoxHeightInner-ACBladeRadius])
                rotate([0, 90, 0])
                    cylinder(h=BoxRadius, r=ACBladeRadius);
        }
    }
    translate([0,-0.5+ACConnectorYOffset-ACConnectorHoleOffsetY,ACConnectorZOffset + ACConnectorHoleOffsetZ])
        rotate([0, 90, 0])
            InsertHolder();
    translate([0,-0.5+ACConnectorHoleDistanceY+ACConnectorYOffset+ACConnectorHoleOffsetY,ACConnectorZOffset + ACConnectorHoleOffsetZ])
        rotate([0, 90, 0])
            InsertHolder();
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

module Box()
{
    difference()
    {
        translate([0, 0, -BoxBottom])
            hull()
            {
                cylinder(h=BoxHeightOuter, r=BoxRadius);
                translate([BoxWidthX, 0, 0])
                    cylinder(h=BoxHeightOuter, r=BoxRadius);
                translate([0, BoxWidthY, 0])
                    cylinder(h=BoxHeightOuter, r=BoxRadius);
                translate([BoxWidthX, BoxWidthY, 0])
                    cylinder(h=BoxHeightOuter, r=BoxRadius);
            }
        cube([BoxWidthX, BoxWidthY, BoxHeightInner]);
    }
}

module SideMounts(createHolepins)
{
    translate([0, 0, BoxHeightInner-CableHolderHeight])
    {
        HolderBase(SideMountWidthX, SideMountWidthY, CableHolderHeight, -2, createHolepins);
    }
    translate([BoxWidthX-SideMountWidthX-BoxRadius, 0, BoxHeightInner-CableHolderHeight])
    {
        HolderBase(SideMountWidthX, SideMountWidthY, CableHolderHeight, -2, createHolepins);
    }
    translate([0, BoxWidthY-BoxRadius, BoxHeightInner-CableHolderHeight])
    {
        rotate([0, 0, -90])
            HolderBase(SideMountWidthX, SideMountWidthY, CableHolderHeight, -2, createHolepins);
    }
}

module BottomMount(createHolepins = false)
{
    translate([BoxWidthX-SideMountWidthX,BoxWidthY-SideMountWidthY,0])
    {
        difference()
        {
            cube([SideMountWidthX+.1, SideMountWidthX+.1, BoxHeightInner]);
            translate([SideMountWidthX/2, SideMountWidthY/2, BoxHeightInner-10])
                cylinder(h=100, d=ScrewDiameterInner);
        }
        if (createHolepins)
        {
            translate([SideMountWidthX/2, SideMountWidthY/2, BoxHeightInner-10])
                cylinder(h=100, d=ScrewDiameterOuter);
        }
    }
}

module CableHolder()
{
    translate([CableHolderXOffset,0, CableHolderZOffset-CableHolderHeight])
    {
        HolderBase(CableHolderWidthX, CableHolderWidthY, CableHolderHeight, 2);
    }
}

module HolderBase(widthX, widthY, height, zOffset, createHolepins)
{
    difference()
    {
        cube([widthX, widthY, height]);
        translate([0,height*1.4,-widthY*2-zOffset])
            rotate([50,0,0])
                cube([widthX, widthY*2, height*2]);
        translate([widthX/2, widthY/2, 0])
            cylinder(h=100, d=ScrewDiameterInner);        
    }
    if (createHolepins)
        translate([widthX/2, widthY/2, 0])
            cylinder(h=100, d=ScrewDiameterOuter); 
}

module CableHole()
{
    translate([0, -5, 0])
        rotate([0, 90, 90])
            hull()
            {
                cylinder(h=20, d=CableHeight);
                translate([0,CableWidth-CableHeight,0])
                    cylinder(h=20, d=CableHeight);
            }
}

module InsertHolders()
{
    translate([InsertOffsetX2, InsertOffsetY1, 0])
        InsertHolder();
    translate([InsertOffsetX3, InsertOffsetY1, 0])
        InsertHolder();
    translate([InsertOffsetX2, InsertOffsetY2, 0])
        InsertHolder();
    translate([InsertOffsetX3, InsertOffsetY2, 0])
        InsertHolder();
}

module InsertHolder()
{
    difference()
    {
        translate([-InsertHolderWidth/2, -InsertHolderWidth/2, 0])
            cube([InsertHolderWidth, InsertHolderWidth, InsertHeight]);
        cylinder(h=InsertHeight, d=InsertDiameter);
    }
}