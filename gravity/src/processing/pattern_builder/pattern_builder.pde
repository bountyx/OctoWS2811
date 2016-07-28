import KinectPV2.KJoint;
import KinectPV2.*;

KinectPV2 kinect;
int [] depthZero;

PImage img;
public int frameWidth = 150*5;
public int frameHeight = 88*5;
int FrameDiag = round(sqrt(pow(frameHeight,2) + pow(frameWidth,2)));
double LastFrameTime = 0;
double frameTime = 0;
int FrameCounterX = 0;
int FrameCounterY = 0;
int FrameCounterDiag = 0;
int PatternIndex = 0;

int framerate = 30;
double PatternTime = 0;
double PatternStart = 0;

int FrameCounterWall_Height = 0;
int FrameCounterRoof_Height = 0;

class Panel {
  int X_Length;
  int Y_Length;  
  int P_index;
  PImage Panel_frame = new PImage();
  Panel(int X, int Y)  //constructor to set X and Y and allocate PImage for pixel array
  {
    X_Length = X; //<>//
    Y_Length = Y;
    Panel_frame = createImage(X_Length, Y_Length, RGB);
    Panel_frame.loadPixels();
  }
}

class Tunnel {
  //constructor that maps dimensions onto each tunnel panel
    Panel Left_Wall;
    Panel Right_Wall;
    Panel Left_Roof;
    Panel Right_Roof;
    Panel Top;
  Tunnel(int wall_height,  int side_roof_Length, int top_width, int Tunnel_length) 
  {
    //index is set in the order of the Teensy's layout
    Left_Wall   = new Panel(Tunnel_length, wall_height);
    Right_Wall  = new Panel(Tunnel_length, wall_height);
    Left_Roof   = new Panel(Tunnel_length, side_roof_Length);
    Right_Roof  = new Panel(Tunnel_length, side_roof_Length);
    Top         = new Panel(Tunnel_length, top_width);   
  }
}

  //define dimensions of the tunnel and use constructor and create global Tunnel Object
  int wall_height = 8*4;
  int roof_length = 8;
  int tunnel_length = 150;
  Tunnel T = new Tunnel(wall_height,roof_length, roof_length, tunnel_length);


void setup() {
  size(400, 400);  
  surface.setSize(frameWidth, frameHeight);
  depthZero    = new int[ KinectPV2.WIDTHDepth * KinectPV2.HEIGHTDepth];
  
  kinect = new KinectPV2(this);
  kinect.enableDepthImg(true);
  kinect.enableSkeleton3DMap(true);
  kinect.init();
}

void draw() {
  background(0);
  int R=0,G=0,B=0;
  double RightHandRaisedRatio = 0;
  double depth_RightHand_Ratio = 0;
  PImage frame = createImage(frameWidth, frameHeight, RGB);
  frame.loadPixels();
  
  ArrayList<KSkeleton> skeletonArray =  kinect.getSkeleton3d();
  int [] DepthRaw = kinect.getRawDepthData();

  //individual JOINTS
  for (int i = 0; i < skeletonArray.size(); i++) {
    KSkeleton skeleton = (KSkeleton) skeletonArray.get(i);
    if (skeleton.isTracked()) {
      KJoint[] joints = skeleton.getJoints();

      PVector RightWristP = joints[KinectPV2.JointType_WristRight].getPosition();
      PVector RightKneeP = joints[KinectPV2.JointType_KneeRight].getPosition();
      PVector HeadP = joints[KinectPV2.JointType_Head].getPosition();   
      double depth = joints[KinectPV2.JointType_WristRight].getZ();
      depth_RightHand_Ratio = depth/4; //4 is as deep as you can go!
      RightHandRaisedRatio =  (RightWristP.y-RightKneeP.y*.85)/(HeadP.y - RightKneeP.y);
      //println(RightWristP);
     
     // println(depth);
     

    }
  }
  
  
  

  //basic patterns that do not use Tunnel object
  //PatternIndex = 8;
  if(false)
  {
    for(int y = 0; y < frameHeight; y++){
      for(int x = 0; x < frameWidth; x++)   
      {
        
        switch(PatternIndex)
        {
        case(0):
          PatternTime = 1000;
          R = 127;
          G = x;
          B = 2*y;
          break;
       case(1):
          PatternTime = 1000;
          R = x;
          G = 2*y;
          B = 0;
        break;
        case(2):
          PatternTime = 1000;
          R = 127;
          G = x;
          B = 0;
        break;
        case(3):
          PatternTime = 1000;
          R = 127;
          G = FrameCounterX;
          B = FrameCounterY;
        break;
        case(4):
          PatternTime = 1000;
          R = FrameCounterX;
          G = x;
          B = FrameCounterY*2;
        break;
        case(5): //animation  x= 0:150, y = 0:88
          PatternTime = 1000*frameWidth/framerate;
          R = 150;
          if(x == FrameCounterX) //horizontal index that counts between 0 and 150 every frame
            G = 150;
          else
            G = 0;
          B = 0;
          break;
        case(6): //animation  x= 0:150, y = 0:88
          PatternTime = 1000*frameHeight/framerate;
          R = 255;
          if(y == FrameCounterY) //verticle index that counts between 0 and 88 every frame
            G = 255;
          else
            G = 0;
          B = 0;
          break;
          case(7): //diagnal
          PatternTime =  1000000/framerate * FrameDiag;
          R = 255;
          if((round(y*FrameDiag/frameHeight) == FrameCounterDiag) && (round(x*FrameDiag/frameWidth) == FrameCounterDiag)) 
            G = 255;
          else
            G = 0;
          B = 0;
          break;
          case(8): //diagnal line commet
          int LineLength = 25;
          PatternTime =  1000000/framerate * FrameDiag;
          int scaledY = round(y*FrameDiag/frameHeight);
          int scaledX = round(x*FrameDiag/frameWidth);
          R = 255;
          G = 0;
          for(int lineIndex = 0; lineIndex <LineLength;lineIndex++)
          {
            if((scaledY == (FrameCounterDiag-lineIndex)) && (scaledX == (FrameCounterDiag-lineIndex)))
              G = 255-10*lineIndex;
          }
          B = 0;
          break;
      }
      frame.pixels[x+frameWidth*y] = color(R,G,B);  // input RGB value for each pixel
      }
      
    }
    frame.updatePixels();  
    image(frame, 0, 0);
  }
  else
  {
    //object oriented animation similar to (6)
    surface.setSize( T.Right_Wall.X_Length, T.Right_Wall.Y_Length+T.Left_Wall.Y_Length+T.Right_Roof.Y_Length+T.Left_Roof.Y_Length+T.Top.Y_Length);
   // T.Right_Wall.Panel_frame.loadPixels();
   // T.Left_Wall.Panel_frame.loadPixels();
    for(int p=0; p<5; p++){  //5 panels
      switch(p)
      {
        case 0: case 4: //walls will be symetrical
        for(int y = 0; y < T.Right_Wall.Y_Length; y++){ //use longest panel (should change this to while loop
          for(int x = 0; x < T.Right_Wall.X_Length; x++)   //use wall x (should change this to while loop
          {
            PatternTime = 1000*frameHeight/framerate;
            R = 255;
            //if(y == FrameCounterWall_Height) //verticle index that counts between 0 and 88 every frame
            if(y== (int)(T.Right_Wall.Y_Length*RightHandRaisedRatio))  //change that Y pixel which is related to the right hand raised ratio
              G = 255;
            else
              G = 0;
            B = 0;
            T.Right_Wall.Panel_frame.pixels[x+T.Right_Wall.X_Length*y] = color(R,G,B);  // input RGB value for each pixel
            T.Left_Wall.Panel_frame.pixels[T.Right_Wall.X_Length*T.Right_Wall.Y_Length -1- (x+T.Right_Wall.X_Length*y)] = color(R,G,B);  //inverted index for left wall
          }
        }
        case 1: case 2: case 3: //roofs
        for(int y = 0; y < T.Right_Roof.Y_Length; y++){ 
          for(int x = 0; x < T.Right_Roof.X_Length; x++)   
          {
            R = 255;
            //if(y == FrameCounterWall_Height) //verticle index that counts between 0 and 88 every frame
            if(x== (int)(T.Right_Roof.X_Length * depth_RightHand_Ratio)) //change that X pixel which is related to the right hand depth ratio (maps Z to X)
              G = 255;
            else
              G = 0;
            B = 0;
            T.Right_Roof.Panel_frame.pixels[x+T.Right_Roof.X_Length*y] = color(R,G,B);  
            T.Left_Roof.Panel_frame.pixels[x+T.Right_Roof.X_Length*y] = color(R,G,B);  
            T.Top.Panel_frame.pixels[x+T.Top.X_Length*y] = color(R,G,B);  
          }
        }  
      } //<>//
    }
    T.Right_Wall.Panel_frame.updatePixels();
    T.Right_Roof.Panel_frame.updatePixels();
    T.Top.Panel_frame.updatePixels();
    T.Left_Roof.Panel_frame.updatePixels();
    T.Left_Wall.Panel_frame.updatePixels();
    
    //integrate the 4 panels into 1 pimage to send to LEDs
    int destination_offset_Y = 0;
    frame.copy(T.Right_Wall.Panel_frame,0,0,
               T.Right_Wall.X_Length,T.Right_Wall.Y_Length,
               0,destination_offset_Y,
               T.Right_Wall.X_Length,T.Right_Wall.Y_Length);
    destination_offset_Y = T.Right_Wall.Y_Length;
    frame.copy(T.Right_Roof.Panel_frame,0,0,
               T.Right_Roof.X_Length,T.Right_Roof.Y_Length,
               0,destination_offset_Y,
               T.Right_Roof.X_Length,T.Right_Roof.Y_Length);               
    destination_offset_Y = T.Right_Wall.Y_Length + T.Right_Roof.Y_Length;
    frame.copy(T.Top.Panel_frame,0,0,
               T.Top.X_Length,T.Top.Y_Length,
               0,destination_offset_Y,
               T.Top.X_Length,T.Top.Y_Length);               
    destination_offset_Y = T.Right_Wall.Y_Length + T.Right_Roof.Y_Length + T.Top.Y_Length;
    frame.copy(T.Left_Roof.Panel_frame,0,0,
               T.Left_Roof.X_Length,T.Left_Roof.Y_Length,
               0,destination_offset_Y,
               T.Left_Roof.X_Length,T.Left_Roof.Y_Length);               
    destination_offset_Y = T.Right_Wall.Y_Length + T.Right_Roof.Y_Length + T.Top.Y_Length + T.Left_Roof.Y_Length;
    frame.copy(T.Right_Wall.Panel_frame,0,0,
               T.Right_Wall.X_Length,T.Right_Wall.Y_Length,
               0,destination_offset_Y,
               T.Right_Wall.X_Length,T.Right_Wall.Y_Length);               
                              
    image(frame, 0, 0);
    /*
    image(T.Right_Wall.Panel_frame, 0, 0);
    image(T.Right_Roof.Panel_frame, 0, T.Right_Wall.Y_Length);
    image(T.Top.Panel_frame, 0, T.Right_Wall.Y_Length + T.  Right_Roof.Y_Length);
    image(T.Left_Roof.Panel_frame, 0, T.Right_Wall.Y_Length + T.Right_Roof.Y_Length+T.Top.Y_Length);
    image(T.Left_Wall.Panel_frame, 0, T.Right_Wall.Y_Length + T.Right_Roof.Y_Length+ T.Left_Roof.Y_Length+ T.Top.Y_Length);
    */
  }
  FrameCounterX++; // used to change the pattern over time (animation) 
  FrameCounterX %= frameWidth;
  FrameCounterY++; // used to change the pattern over time (animation)
  FrameCounterY %= frameHeight;
  FrameCounterDiag++;
  FrameCounterDiag %= FrameDiag;
  
  //object oriented counters
  FrameCounterWall_Height++;
  FrameCounterWall_Height %= T.Right_Wall.Y_Length;
  FrameCounterRoof_Height++;
  FrameCounterRoof_Height %= T.Right_Roof.Y_Length;

  if(millis() - PatternStart > PatternTime)  //go to next pattern and reset counters after pattern completes
  {
    PatternIndex++;
    PatternIndex = PatternIndex % 9; //8 patterns only
    FrameCounterX = 0;
    FrameCounterY = 0;
    PatternStart = millis();
  }
  
  //control delay between next frame to maintain target framerate
  frameTime = millis();  
  while(frameTime-LastFrameTime<1000/framerate)
  {
    delay(1);
    frameTime = millis();
  }
  LastFrameTime = millis();
}