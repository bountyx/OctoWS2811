PFont f;
PShader blur;

int R = (int)random(255);
int G = (int)random(255);
int B = (int)random(255);


int dR = 1; // starting value
int dG = 5; // starting value
int dB = 3; // starting value


void setup()
{
  size(600, 128, P2D);

  // Horizontal blur, from the SepBlur Processing example
  blur = loadShader("blur.glsl");
  blur.set("blurSize", 20);
  blur.set("sigma", 8.0f);
  blur.set("horizontalPass", 1);
  // Create the font
  f = createFont("Futura", height*3/4);
  textFont(f);
}

void scrollMessage(String s, float speed)
{
  int x = int( width + (millis() * -speed) % (textWidth(s) + width) );
  text(s, x, height*3/4);  
}


 
void draw()
{
  background(0);
  R+=dR;
  G+=dG;    
  B+=dB;
  
  if ((R <= 0) || (R >= 255))  // if out of bounds
    dR = - dR; // swap direction  
  if ((G <= 0) || (G >= 255))  // if out of bounds
    dG = - dG; // swap direction  
  if ((B <= 0) || (B >= 255))  // if out of bounds
    dB = - dB; // swap direction  

  fill(R,G,B);
  scrollMessage("<< Gravity >>", 0.05);
  ellipseMode(CENTER);
  float wh = random(3, 10);
  int whvalue = int(wh);
  // get random x axis point; need to figure out how to test bounds outside or touching window
  float x = random(0, width);
  int xx = int(x);
    // get random y axis point; need to figure out how to test bounds outside or touching window
  float y = random(0, height);
  int yy= int(y);
  fill(255,255,255);

  ellipse(xx, yy, whvalue, whvalue);

  
//  filter(blur);
}