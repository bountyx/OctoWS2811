import ddf.minim.*; 
import processing.video.*;
import processing.core.*;

public class PlayVideo extends PApplet {

    float textScale;
    int   width;
    int   height;
    Movie  movie;
   

    public PlayVideo(int w, int h, Movie m) {
        width  = w;
        height = h;
        movie = m;
       }

    public void settings() {
        fullScreen(2);
    }

  public void setup () {
  background(0);
  movie.play();
  }  
  
  void movieEvent(Movie movie) {
    movie.read();
  }
  public void draw () {
  //image(movie, 0, 0, displayWidth, displayHeight);
  image(movie, 0, 0);
  }
}