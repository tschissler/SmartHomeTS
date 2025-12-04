$fn=40;

Width_y = 70;
Height = 27;
Wall = 3;
Bottom = 1.5;

PCB_Width_x = 60;
PCB_Width_y = 40;
PCB_Height = 1.5;
PCB_Offset_z = 3;
PCB_Holder = 4.5;

Cable_Diameter = 7;
Cable_Distance = 7;
Cable_Offset_z = Height/2+5;
CableCount = 4;

USBConnector_Width = 11;
USBConnector_Height = 6.5;
USBConnector_Offset_y = Wall + USBConnector_Height/2 + 3 + 9.25;
USBConnector_Offset_z = 15.9 + PCB_Offset_z + Bottom;

Width_x = PCB_Width_x + 2*Wall;
Cable_Offset_x = (Width_x-Cable_Diameter*CableCount-Cable_Distance*(CableCount-1))/2;
echo("Cable Offset X: ", Cable_Offset_x);
rounded_box();

module rounded_box() {
    difference() {
        hull() {
            translate([Wall, Wall, 0])
                cylinder(h = Height, r = Wall);
            translate([Wall, Width_y-Wall, 0])
                cylinder(h = Height, r = Wall);
            translate([Width_x-Wall, Wall, 0])
                cylinder(h = Height, r = Wall);
            translate([Width_x-Wall, Width_y-Wall, 0])
                cylinder(h = Height, r = Wall);
        }
        translate([Wall, Wall, Bottom+PCB_Height + PCB_Offset_z])
            cube([Width_x-2*Wall, Width_y-2*Wall, Height]);
        translate([Wall, Wall, Bottom + PCB_Offset_z])
            cube([Width_x-2*Wall, PCB_Width_y, PCB_Height]);
        translate([Wall+PCB_Holder, Wall, Bottom])
            cube([PCB_Width_x-2*PCB_Holder, PCB_Width_y, PCB_Offset_z]);

        for (i = [0 : CableCount - 1]) {
            translate([Cable_Offset_x + i * (Cable_Diameter + Cable_Distance) + Cable_Diameter/2, Width_y+1, Cable_Offset_z])
                rotate([90, 0, 0])
                    cylinder(h = 10, d = Cable_Diameter);
        }
        // translate([30, 0,0])
        //     cube([100, 100, 100]);

        usbConnector();
    }
}

module usbConnector() {
    translate([0, USBConnector_Offset_y, USBConnector_Offset_z])
    rotate([0, 90, 0]) {
        hull() {
            translate([0, 0, -1])
                cylinder(h = 10, d = USBConnector_Height);
            translate([0, USBConnector_Width - USBConnector_Height, -1])
                cylinder(h = 10, d = USBConnector_Height);
        }
    }
}

