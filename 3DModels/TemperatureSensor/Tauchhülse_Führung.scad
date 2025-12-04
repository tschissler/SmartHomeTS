$fn=100;

Ueberstand_Sensor = 0.3;
Tauchhuelse_Durchmesser = 12-Ueberstand_Sensor;

Fuehrung_Laenge = 50;

Sensor_Durchmesser = 7;
Sensor_Laenge = 40;
Kabel_Durchmesser = 5;

difference() {
    translate([Tauchhuelse_Durchmesser/2, 0, 0])
        cylinder(h=Fuehrung_Laenge, d=Tauchhuelse_Durchmesser);
    translate([Sensor_Durchmesser/2-Ueberstand_Sensor, 0, 0])
    {
        cylinder(h=Sensor_Laenge, d=Sensor_Durchmesser);
        cylinder(h=100, d=Kabel_Durchmesser);
    }
}