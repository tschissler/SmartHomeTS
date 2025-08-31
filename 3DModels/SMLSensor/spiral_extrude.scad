//Copyright 2018 Douglas Peale
//This transformation extrudes a two dimmenional shape in a spiral. It fills the gap between linear_extrude and rotate_extrude
//This is released under the GPL version 3.0
//
// Note that this uses linear_extrude to create segments of the spiral and piece them together. Twist is used to get the pieces to mate up properly.
// This makes the job much more complicated than it would have been if I could get access to the data points of the 2D objects this function is passed.
// Version 2.0
// Add capability of having Height=0 for flat spirals
// Fix Starts so that it actually works
// Version 3.0
// Reimplemented this function to simplify it, and allow circular and circular arc extrudes.
// fixed bug that prevented acurate heights and arc lengths from being implemented (previously an integer number of segments was used).
// Wrote wraper function with old name for backwards compatibility.
// Version 3.1
// fixed a bug where one more step than requested was added to the spiral.

//Simple Example
    //Multiple shapes, start and end radius the same
spiral_extrude(Radius=19, EndRadius=19, Pitch=10, Height=30, StepsPerRev=50, Starts=1){circle($fn=20,1);translate([10,0,0])rotate([0,0,20])square([5,2],center=true);}

    //Start and End radius different, negative pitch
translate([0,60,0])spiral_extrude(Radius=19, EndRadius=5, Pitch=-10, Height=30, StepsPerRev=50, Starts=1){rotate([0,0,20])square([5,2],center=true);}

    //Flat Spiral
translate([60,0,0])spiral_extrude(Radius=10, EndRadius=20, Pitch=3, Height=0, StepsPerRev=50, Starts=1){scale([1,2])circle($fn=20,1);}

    //Multiple Starts
translate([0,-60,0])spiral_extrude(Radius=19, EndRadius=5, Pitch=20, Height=30, StepsPerRev=50, Starts=3){rotate([0,0,20])square([5,2],center=true);}

    //Circular
translate([-60,0,0])extrude_spiral(StartRadius=10, Angle=360, RPitch=0, ZPitch=0, StepsPerRev=50){
    square([1,5]);
}
    //Circular arc
translate([-50,-50,0])extrude_spiral(StartRadius=10, Angle=234.25, RPitch=0, ZPitch=0, StepsPerRev=36){
    square([1,5]);
}

//Transforms a two dimmensional object in the XY plane to a spiral extrusion with a cross section perpendicular to the extrusion of the original two dimmensional object.
// Radius is the inital radius of the spiral.
// EndRadius is the final radius of the spiral. If not specified, it is the same as Radius.
// Spiral proceeds counter clockwise from start radius to end radius.
// Pitch is the change in Z per revolution of the spiral (this can be negative) unless Height is zero, in which case Pitch is the change in Radius/revolution.
// Starts is the number of spirals.
// Height is the total height of the spiral (this should be positive), can be zero. If pitch is positive, height goes up, if pitch is negative, height goes down
// StepsPerRev is the number of linear extruded segments per revolution of the spiral. A larger number generates a smoother spiral

    //Wraper function for backwords compatibility.
module spiral_extrude(Radius=1,EndRadius=-1,Pitch=50,Starts=1,Height=1,StepsPerRev=50){
    Angle=abs((Height!=0)?360*Height/Pitch:360*(Radius-EndRadius)/Pitch);
    ZPitch=(Height!=0)?Pitch:0;
    RPitch=(Height!=0)?((EndRadius!=-1)?(EndRadius-Radius)/abs(Height/Pitch):0):Pitch;
    
    extrude_spiral(StartRadius=Radius, Angle=Angle, ZPitch=ZPitch, RPitch=RPitch, StepsPerRev=StepsPerRev,Starts=Starts)children();
}


    // Start radius is the radius at which the extrude will start.
    // Angle is the number of degrees of rotation of the extrusion. This is always counter clock wise about the z axis.
    // ZPitch is the change in height per revolution, can be negative.
    // RPitch is the change in radius per revolution, can be negative.
    // StepsPerRev is the number of segments that will be drawn per revolution. Note that the last segment can be a partial segment.
    // Starts is the number of equally spaced copies of the spiral that will be drawn.
module extrude_spiral(StartRadius=10,Angle=360,ZPitch=0,RPitch=0,StepsPerRev=50,Starts=1){
    NumberOfSteps=ceil(Angle/360*StepsPerRev)-1;
        //Number of degrees of last step to use.
    Remainder=((Angle/360*StepsPerRev)-floor(Angle/360*StepsPerRev));
    for(i=[0:NumberOfSteps]){
        for(j=[0:Starts-1]){
            rotate([0,0,360*i/StepsPerRev+360*j/Starts]){
                    //Current Radius
                LocRadius=StartRadius+i*RPitch/StepsPerRev;
                    //Radius at next junction
                LocRadiusJoint=LocRadius/cos(360/(2*StepsPerRev))-RPitch*360/StepsPerRev/2;
                    //Length of line connecting endpoints of the current radius and the radius of the next joint
                TaperLen=sqrt(pow(LocRadius,2)+pow(LocRadiusJoint,2)-2*LocRadius*LocRadiusJoint*cos(360/(2*StepsPerRev)));
                    //Angle of above line relative to the current radius
                TaperAngle=90-asin(max(min(sin(360/(2*StepsPerRev))*LocRadiusJoint/TaperLen,1),-1));
 
                translate([LocRadius,0,i*ZPitch/StepsPerRev]){
                    segment(Radius=LocRadius, ZPitch=ZPitch, StepsPerRev=StepsPerRev, First=i==0, Last=(i==NumberOfSteps)?Remainder:0,RPitch=RPitch)children();
                }
            }
        }
    }
}

    //This module builds the segments that make up the spiral
module segment(Radius=1, ZPitch=1, StepsPerRev=10,First=false,Last=false,RPitch=0){
    render(){
            //Length of the extrusion needed to cover the angle determined by StepsPerRev
        LengthOfSegment=Radius*2*sin(360/(2*StepsPerRev))/cos(atan2(ZPitch,(2*PI*Radius)));
        
            //angle of extrusion relative to the XY plane
        SlopeAngle=atan2(ZPitch,(2*PI*Radius));
        
            //Radius Change per step
        RChange=RPitch/StepsPerRev;
            //Angle change per step
        StepAngle=360/StepsPerRev;
            //Stuff for calculating the angle relative to a radial line
        Opposite=Radius-(Radius+RChange)*cos(StepAngle);
        Adjacent=(Radius+RChange)*sin(StepAngle);
        
        SpiralAngle=atan2(Opposite,Adjacent);
            
        difference(){
            rotate([90+SlopeAngle,0,SpiralAngle]){
                
                    //Make extrusion 20 times as long as the gap to allow trimming to proper angle
                    //twist 20 times as much to account for 20 times the length
                    //I think the twist should be -20*360/StepsPerRev*sin(SlopeAngle), but for some reason -18 works better.
                rotate([0,0,-0.5*19*360/StepsPerRev*sin(SlopeAngle)])linear_extrude(center=true,height=20*LengthOfSegment,twist=-13.5*360/StepsPerRev*sin(SlopeAngle),convexity=10)children();
            }
                //Cut the ends of the extrusion to the proper angle for mating. Cubes are really big because I can't figure out how to ask OpenSCAD how big the 2D object is that I am extruding.
            translate([0,Last?Radius*tan(Last*360/StepsPerRev):Radius*tan(360/StepsPerRev),Last?Last*ZPitch/StepsPerRev:ZPitch/StepsPerRev])rotate([SlopeAngle,0,Last?Last*360/StepsPerRev:360/StepsPerRev]) translate([-5000,0,-5000])cube(10000);
            translate([0,0,0])rotate([SlopeAngle,0,0]) translate([0,-5000.01,0])cube(10000,center=true);
        }    
    }
}