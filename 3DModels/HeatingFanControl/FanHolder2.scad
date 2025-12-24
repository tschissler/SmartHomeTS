$fn=60;

Wall = 3;
RadiatorThickness = 3.5;
RadiatorHeight = 6;
RadiatorGuard = RadiatorThickness+2*Wall;

MagnetDiameter = 6;

FanDiameter = 120;

Width_X = 153;
Height = 26;

OuterWidth_x = Width_X + Wall * 2;
OuterWidth_y = FanDiameter+Wall;
OuterHeight = Height + Wall/2;

CanbleOffsetX = 20;
CableWidth = 7.5;
CableHeight = 10;

difference()
{
    cube([OuterWidth_x, OuterWidth_y, OuterHeight]);
    translate([OuterWidth_x/2, OuterWidth_y/2, -1])
        cylinder(h=100, d=FanDiameter);

    translate([(OuterWidth_x-FanDiameter)/2, (OuterWidth_y-FanDiameter)/2, Wall/2])
        cube([FanDiameter, FanDiameter, 100]);

    translate([CanbleOffsetX+(OuterWidth_x-FanDiameter)/2, 0, OuterHeight-CableHeight])
        cube([CableWidth, 20, CableHeight]);
}

RadiatorGuard(0);
RadiatorGuard(OuterWidth_x - RadiatorGuard);

module RadiatorGuard(xOffset) 
{
    translate([xOffset, 0, OuterHeight])
    {
        difference()
        {
            cube([RadiatorGuard, OuterWidth_y, RadiatorHeight]);
            translate([Wall, 0, 0])
                cube([RadiatorThickness, OuterWidth_y, RadiatorHeight+1]);
            translate([RadiatorGuard/2, 10, 0])
                cylinder(h=100, d=MagnetDiameter);
            translate([RadiatorGuard/2, OuterWidth_y-10, 0])
                cylinder(h=100, d=MagnetDiameter);
            translate([RadiatorGuard/2, 40, 0])
                cylinder(h=100, d=MagnetDiameter);
            translate([RadiatorGuard/2, OuterWidth_y-40, 0])
                cylinder(h=100, d=MagnetDiameter);

        }
    }
}
