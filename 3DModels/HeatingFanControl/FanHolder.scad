$fn=100;

Fan_Diameter = 76;
Fan_Outer = 80.5;
Fan_Height = 25;

InnerWidth_x = 102;
InnerWidth_y = 34+Fan_Height;
Height = 10;
Wall = 4;
Bottom = 2;
Clamp = 5;

Screw_Diameter = 4;
Screw_Distance = 67.2 + Screw_Diameter;

OuterWidth_x = InnerWidth_x + 2*Wall;
OuterWidth_y = InnerWidth_y + 2*Bottom;

difference() {
    Plate();

    // uncomment for endpiece
    translate([0, -100 - Height/2, -0.1])
        cube([200, 100, 100]);
    translate([Wall, -100, Bottom])
        cube([InnerWidth_x, 100, 10]);
}

translate([0,Fan_Outer,0]) {
    Plate();
}

module Plate() {
    difference() {
        Clamp();

        translate([InnerWidth_x/2+Wall, -Fan_Outer/2, -0.5])
            cylinder(h=Wall+1, d=Fan_Diameter);
        translate([InnerWidth_x/2+Wall, Fan_Outer/2, -0.5])
            cylinder(h=Wall+1, d=Fan_Diameter);
    }
    ScrewHoles();
}

module ScrewHoles() {
    translate([OuterWidth_x/2-Screw_Distance/2, Fan_Outer/2-Screw_Distance/2, 0])
        cylinder(h=10, d=Screw_Diameter);
    translate([OuterWidth_x/2+Screw_Distance/2, Fan_Outer/2-Screw_Distance/2, 0])
        cylinder(h=10, d=Screw_Diameter);
    translate([OuterWidth_x/2-Screw_Distance/2, -Fan_Outer/2+Screw_Distance/2, 0])
        cylinder(h=10, d=Screw_Diameter);
    translate([OuterWidth_x/2+Screw_Distance/2, -Fan_Outer/2+Screw_Distance/2, 0])
        cylinder(h=10, d=Screw_Diameter);
}

module Clamp() {
    translate([0,Height/2, 0]) {
        rotate([90, 0, 0]) {
            difference()
            {
                cube([OuterWidth_x, OuterWidth_y, Height]);
                translate([Wall, Bottom, -1])
                    cube([InnerWidth_x, InnerWidth_y, Height+2]);
                translate([Wall+Clamp, Bottom, -1])
                    cube([InnerWidth_x-2*Clamp, InnerWidth_y+Wall+1, Height+2]);
            }
            Skew(-90);
            translate([InnerWidth_x,0,0])
                Skew(180);
        }
    }
    translate([0,-Fan_Outer/2,0])
        cube([OuterWidth_x, Fan_Outer, Bottom]);
}

module Skew(rotation) {
    translate([Wall, Wall+InnerWidth_y, 0]) {
        rotate([0,0,rotation]) {
            linear_extrude(height=Height) {
                polygon(points=[[0,0],[Clamp,0],[0,Clamp]]);
            }
        }
    }
}