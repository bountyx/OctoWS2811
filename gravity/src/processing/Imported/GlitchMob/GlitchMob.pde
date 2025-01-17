// Some real-time FFT! This visualizes music in the frequency domain using a
// polar-coordinate particle system. Particle size and radial distance are modulated
// using a filtered FFT. Color is sampled from an image.

import ddf.minim.analysis.*;
import ddf.minim.*;
import processing.video.*;

PImage dot;
PImage colors;
TriangleGrid triangle;
Minim minim;
AudioInput in;
FFT fft;
Movie myMovie;


float[] fftFilter;
float spin = 0.003;
float radiansPerBucket = radians(2);
float decay = 0.96;
float opacity = 20;
float minSize = 0.1;
float sizeScale = 0.5;
float md;
float mt;

void setup()
{
  size(300, 300, P3D);

  minim = new Minim(this); 

  // Small buffer size!
  in = minim.getLineIn();

  fft = new FFT(in.bufferSize(), in.sampleRate());
  fftFilter = new float[fft.specSize()];

  dot = loadImage("dot.png");
  colors = loadImage("colors.png");

  // Map our triangle grid to the center of the window
  triangle = new TriangleGrid();
  triangle.grid16();
  triangle.mirror();
  triangle.rotate(radians(60));
  triangle.scale(height * 0.2);
  triangle.translate(width * 0.5, height * 0.5);
  
  myMovie = new Movie(this, "The Glitch Mob_Beyond Monday.mp4");
  PApplet sketch = new PlayVideo(900,900, myMovie);
  String[] args = { "PlayVideo", };          
  PApplet.runSketch(args, sketch);
  
  
  

}

void draw()
{
  background(0);
  md = myMovie.duration();
  mt = myMovie.time(); 
  
  
  if(mt>3)
  {

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
   
      image(dot, center.x - size/2, center.y - size/2, size, size);
      fill(rgb);
      ellipseMode(CORNER);
      ellipse(center.x - size/2, center.y - size/2, size/15, size/15);
      ellipse(center.x + size/2, center.y - size/2, size/15, size/15);
      ellipse(center.x - size/2, center.y + size/2, size/15, size/15);
      ellipse(center.x + size/2, center.y + size/2, size/15, size/15);
      ellipse(center.x + size/2, center.y + size/2, size/15, size/15);
    }
  }
}

void movieEvent(Movie movie) {
  movie.read();
}