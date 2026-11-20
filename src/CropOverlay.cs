using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LkScreenshot {
 internal sealed class CropOverlay : Canvas {
  public Rect Area { get; private set; }
  readonly double imageWidth, imageHeight;
  readonly Rectangle shade = new Rectangle {Fill=new SolidColorBrush(Color.FromArgb(145,0,0,0)),IsHitTestVisible=false};
  readonly Rectangle frame = new Rectangle {Stroke=Brushes.White,IsHitTestVisible=false};
  readonly Thumb[] handles=new Thumb[8];
  readonly Thumb[] edges=new Thumb[4];
  double handleSize=12;
  public CropOverlay(double width,double height) {
   imageWidth=Width=width;imageHeight=Height=height;
   Area=new Rect(0,0,width,height);
   Children.Add(shade);Children.Add(frame);
   int index=0;
   for(int y=0;y<3;y++)for(int x=0;x<3;x++) {
    if(x==1&&y==1)continue;
    int horizontal=x,vertical=y;
    var thumb=new Thumb {Background=Brushes.White,Cursor=x==1?Cursors.SizeNS:y==1?Cursors.SizeWE:x==y?Cursors.SizeNWSE:Cursors.SizeNESW};
    var border=new FrameworkElementFactory(typeof(Border));
    border.SetValue(Border.BackgroundProperty,Brushes.White);
    border.SetValue(Border.BorderBrushProperty,Brushes.Black);
    border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
    thumb.Template=new ControlTemplate(typeof(Thumb)) {VisualTree=border};
    WireDrag(thumb,horizontal,vertical);
    handles[index++]=thumb;Children.Add(thumb);
   }
   PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.OriginalSource==this)e.Handled=true;};
   for(int i=0;i<4;i++) {
    int horizontal=i<2?i*2:1,vertical=i<2?1:(i-2)*2;
    var edge=new Thumb {Cursor=i<2?Cursors.SizeWE:Cursors.SizeNS};
    var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);
    edge.Template=new ControlTemplate(typeof(Thumb)) {VisualTree=hit};
    WireDrag(edge,horizontal,vertical);edges[i]=edge;Children.Insert(2,edge);
   }
   Draw();
  }
  void WireDrag(Thumb thumb,int horizontal,int vertical) {
    thumb.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){e.Handled=true;thumb.CaptureMouse();};
    Point previous=new Point();
    thumb.GotMouseCapture+=delegate {previous=Mouse.GetPosition(this);};
    thumb.MouseMove+=delegate {
     if(!thumb.IsMouseCaptured)return;
     var point=Mouse.GetPosition(this);
     Resize(horizontal,vertical,point.X-previous.X,point.Y-previous.Y);
     previous=point;
    };
    thumb.PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){thumb.ReleaseMouseCapture();e.Handled=true;};
  }
  public void SetScale(double scale) {handleSize=12/Math.Max(0.01,scale);Draw();}
  internal void Resize(int horizontal,int vertical,double dx,double dy) {
   double left=Area.Left,top=Area.Top,right=Area.Right,bottom=Area.Bottom;
   if(horizontal==0)left=Math.Max(0,Math.Min(right-1,left+dx));
   if(horizontal==2)right=Math.Min(imageWidth,Math.Max(left+1,right+dx));
   if(vertical==0)top=Math.Max(0,Math.Min(bottom-1,top+dy));
   if(vertical==2)bottom=Math.Min(imageHeight,Math.Max(top+1,bottom+dy));
   Area=new Rect(new Point(left,top),new Point(right,bottom));Draw();
  }
  void Draw() {
   shade.Width=imageWidth;shade.Height=imageHeight;
   var cutout=new GeometryGroup {FillRule=FillRule.EvenOdd};
   cutout.Children.Add(new RectangleGeometry(new Rect(0,0,imageWidth,imageHeight)));
   cutout.Children.Add(new RectangleGeometry(Area));shade.Clip=cutout;
   SetLeft(frame,Area.Left);SetTop(frame,Area.Top);frame.Width=Area.Width;frame.Height=Area.Height;frame.StrokeThickness=handleSize/8;
   for(int i=0;i<4;i++) {
    var edge=edges[i];bool side=i<2;
    edge.Width=side?handleSize:Area.Width;
    edge.Height=side?Area.Height:handleSize;
    SetLeft(edge,side?Math.Max(0,Math.Min(imageWidth-handleSize,(i==0?Area.Left:Area.Right)-handleSize/2)):Area.Left);
    SetTop(edge,side?Area.Top:Math.Max(0,Math.Min(imageHeight-handleSize,(i==2?Area.Top:Area.Bottom)-handleSize/2)));
   }
   int index=0;
   for(int y=0;y<3;y++)for(int x=0;x<3;x++) {
    if(x==1&&y==1)continue;
    var handle=handles[index++];handle.Width=handle.Height=handleSize;
    SetLeft(handle,Math.Max(0,Math.Min(imageWidth-handleSize,Area.Left+Area.Width*x/2-handleSize/2)));
    SetTop(handle,Math.Max(0,Math.Min(imageHeight-handleSize,Area.Top+Area.Height*y/2-handleSize/2)));
   }
  }
 }
}
