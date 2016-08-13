/* OpenProcessing Tweak of *@*http://www.openprocessing.org/sketch/70780*@* */
/* !do not delete the line above, required for linking your tweak if you upload again */
TriangleGrid triangle;
import ddf.minim.*; 
import ddf.minim.signals.*;
import ddf.minim.analysis.*;
import processing.video.*;

Movie myMovie;
Minim minim;
AudioInput in;
FFT fft;
float[] fftFilter;
BeatDetect beat;
PImage GravityPic;
PImage colors;

float md;
float mt;
void setup () {
  size (750,160, P3D) ;
  //surface.setSize(500,500);
  minim = new Minim(this);
  in = minim.getLineIn();
  fft = new FFT(in.bufferSize(), in.sampleRate());
  fftFilter = new float[fft.specSize()];
  beat = new BeatDetect();
  //GravityPic = loadImage("Gravity_Sign.jpg");
  GravityPic = loadImage("dot.png");
  colors = loadImage("colors.png");
  //myMovie = new Movie(this, "The Glitch Mob_Beyond Monday.mp4");
  //PApplet sketch = new PlayVideo(900,900, myMovie);
  //String[] args = { "PlayVideo", };          
  //PApplet.runSketch(args, sketch);

    // Map our triangle grid to the center of the window
  triangle = new TriangleGrid();
  triangle.grid16();
  triangle.mirror();
  triangle.rotate(radians(60));
  triangle.scale(height * 0.2);
  triangle.translate(width * 0.5, height * 0.5);
  
  // This code will print all the lines from the source text file.
//  String[] lines = loadStrings("PatternList.txt");
  //println("there are " + lines.length + " lines");
 // println(lines);  
}
//void movieEvent(Movie movie) {
//  movie.read();
//}
float spin = 0.003;
float radiansPerBucket = radians(2);
float decay = 0.96;
float opacity = 20;
float minSize = 0.1;
float sizeScale = 0.5;
float eRadius;

void draw () {
  background(0);
  beat.detect(in.mix);
  //md = myMovie.duration();
  //mt = myMovie.time(); 
  //println(mt);
  delay(1);

    
  
  
  int t1 = 1;
  if(true)
  {
    color c = 0;
   // circle();
       fft.forward(in.mix);
    for (int i = 0; i < fftFilter.length; i++) {
      fftFilter[i] = max(fftFilter[i] * decay, log(1 + fft.getBand(i)) * (1 + i * 0.01));
    }
    
    for (int i = 0; i < fftFilter.length; i += 3) {   
  
      color rgb = colors.get(int(map(i, 0, fftFilter.length-1, 0, colors.width-1)), colors.height/2);
      tint(rgb, fftFilter[i] * opacity);
      blendMode(ADD);
   
      float size = height * (minSize + sizeScale * fftFilter[i]);
      PVector center = new PVector(width * (fftFilter[i] * 0.2), 0);
      center.rotate(millis() * spin + i * radiansPerBucket);
      center.add(new PVector(width * 0.5, height * 0.5));
  //      println(center);
   //   println(size);
       
      //image(GravityPic, center.x - width/2, center.y - height/2, size/5, size/5);
   //   println( "xcenter = " + (center.x - size/2));
  //    println( "ycenter = " + (center.y - size/2));
 //     println("size = " + size/5);
      ellipse( center.x - width/2, center.y - height/2, size/5, size/5);
    //  image(GravityPic, center.x - size/2, center.y - size/2, size, size);
    }   
   
   
  }
  else
  {
    if ( beat.isOnset() ) eRadius = 80;
    ellipse(width/2, height/2, eRadius, eRadius);
    eRadius *= 0.95;
    if ( eRadius < 20 ) 
      eRadius = 20;
  }
}


void flash(color c)
  {
    
  }
  

  void circle()
  {
    fft.forward(in.mix);
    for (int i = 0; i < fftFilter.length; i++) {
      fftFilter[i] = max(fftFilter[i] * decay, log(1 + fft.getBand(i)) * (1 + i * 0.01));
    }
    
    for (int i = 0; i < fftFilter.length; i += 3) {   
      color rgb = colors.get(int(map(i, 0, fftFilter.length-1, 0, colors.width-1)), colors.height/2);
      float size = height * (minSize + sizeScale * fftFilter[i]);
      tint(rgb, fftFilter[i] * opacity);
      blendMode(ADD);
      PVector center = new PVector(width * (fftFilter[i]), 0);
      center.rotate(millis() * spin + i * radiansPerBucket);
      center.add(new PVector(width * 0.5, height * 0.5));
      println(center);
   //   println(size);
       
      //image(GravityPic, center.x - width/2, center.y - height/2, size/5, size/5);
      println( "xcenter = " + (center.x - size/2));
      println( "ycenter = " + (center.y - size/2));
      println("size = " + size/5);
     // ellipse( center.x - width/2, center.y - height/2, size/5, size/5);
      image(GravityPic, center.x - size/2, center.y - size/2, size, size);
    }   
  }
  
    



void keyPressed () {
   if (key=='s') {
  //   saveFrame("exports/img-####.tiff") ; // to save the frames you want
     
   }
}