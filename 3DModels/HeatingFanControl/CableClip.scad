

InnerWidth_x = 106.5;
InnerWidth_y = 43.5;
Height = 10;
Wall = 4;
Clamp = 5;

OuterWidth_x = InnerWidth_x + 2*Wall;
OuterWidth_y = InnerWidth_y + 2*Wall;

difference()
{
    cube([OuterWidth_x, OuterWidth_y, Height]);
    translate([Wall, Wall, -1])
        cube([InnerWidth_x, InnerWidth_y, Height+2]);
    translate([Wall+Clamp, -1, -1])
        cube([InnerWidth_x-2*Clamp, InnerWidth_y, Height+2]);
}

Schraege();
translate([OuterWidth_x,0,0])
    rotate([0,0,90])
        Schraege();


module Schraege() {
    translate([Wall, Wall, 0]) {
        linear_extrude(height=Height) {
            polygon(points=[[0,0],[Clamp,0],[0,Clamp]]);
        }
    }
}

