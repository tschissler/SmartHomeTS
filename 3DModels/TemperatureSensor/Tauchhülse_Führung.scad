$fn=100;

Ueberstand_Sensor = 0.5;
Tauchhuelse_Durchmesser = 12-Ueberstand_Sensor;

Fuehrung_Laenge = 30;

Sensor_Durchmesser = 6;
Sensor_Laenge = 20;
Kabel_Durchmesser = 3;

difference() {
    translate([Tauchhuelse_Durchmesser/2, 0, 0])
        cylinder(h=Fuehrung_Laenge, d=Tauchhuelse_Durchmesser);
    translate([Sensor_Durchmesser/2-Ueberstand_Sensor, 0, 0])
    {
        cylinder(h=Sensor_Laenge, d=Sensor_Durchmesser);
        cylinder(h=100, d=Kabel_Durchmesser);
    }
}