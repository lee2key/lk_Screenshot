using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LkScreenshot {
 internal sealed class InlineText : Border {
  readonly Canvas canvas;
  readonly TextBlock label;
  readonly Brush backdrop=new SolidColorBrush(Color.FromArgb(100,0,0,0));
  Point dragStart, positionStart;
  bool dragging;
  readonly Action<InlineText> edit, move;
  internal TextBox Input {get;private set;}
  internal bool Editing {get {return Child==Input;}}
  public bool Dragging {get {return IsMouseCaptured;}}
  public Point Position {
   get {return new Point(Canvas.GetLeft(this),Canvas.GetTop(this));}
   set {Canvas.SetLeft(this,value.X);Canvas.SetTop(this,value.Y);}
  }
  public string Text {
   get {return Input.Text;}
   set {Input.Text=value;label.Text=value;}
  }

  public InlineText(Canvas canvas,Point position,Brush ink,FontFamily font,Action<InlineText> edit,Action<InlineText> move) {
   this.canvas=canvas;
   this.edit=edit;this.move=move;
   Background=Brushes.Transparent;
   BorderThickness=new Thickness(1);
   MaxWidth=canvas.Width;
   MaxHeight=canvas.Height;
   ClipToBounds=true;
   Input=new TextBox {
    Style=null,MinWidth=36,FontFamily=font,FontSize=24,FontWeight=FontWeights.SemiBold,
    Foreground=ink,Background=backdrop,BorderThickness=new Thickness(0),Padding=new Thickness(3),
    CaretBrush=Theme.Brush("text"),TextWrapping=TextWrapping.Wrap,AcceptsReturn=true
   };
   label=new TextBlock {
    FontFamily=font,FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=ink,
    Background=backdrop,Padding=new Thickness(3),TextWrapping=TextWrapping.Wrap,
    MaxWidth=Math.Max(0,canvas.Width-2),IsHitTestVisible=false
   };
   Position=position;
   SizeChanged+=delegate {if(Editing)MoveTo(Position);};
   PreviewMouseLeftButtonDown+=StartDrag;
   MouseMove+=delegate(object sender,MouseEventArgs e) {
    if(Dragging)UpdateDrag(e);
   };
   PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) {
    if(!Dragging)return;
    UpdateDrag(e);ReleaseMouseCapture();e.Handled=true;
   };
   LostMouseCapture+=delegate {
    if(!dragging)return;
    dragging=false;
    var oldPosition=positionStart;
    if(Position!=oldPosition)Moved(oldPosition);
   };
   FinishTyping();
  }

  public event Action<Point> Moved=delegate {};

  void UpdateDrag(MouseEventArgs e) {
   var delta=e.GetPosition(canvas)-dragStart;
   if(!dragging && delta.Length<1)return;
   if(!dragging)move(this);
   dragging=true;MoveTo(positionStart+delta);
  }

  void StartDrag(object sender,MouseButtonEventArgs e) {
   if(Editing)return;
   e.Handled=true;
   if(e.ClickCount==2) {
    edit(this);
    return;
   }
   dragStart=e.GetPosition(canvas);
   positionStart=Position;
   CaptureMouse();
  }

  internal void MoveTo(Point position) {
   Position=new Point(Math.Max(0,Math.Min(position.X,canvas.Width-ActualWidth)),Math.Max(0,Math.Min(position.Y,canvas.Height-ActualHeight)));
  }

  public void StartTyping() {
   Child=Input;BorderBrush=Theme.Brush("button-focus");Cursor=Cursors.IBeam;ToolTip=null;
   Input.Focus();Input.CaretIndex=Input.Text.Length;
  }

  public void FinishTyping() {
   label.Text=Input.Text;
   Child=label;
   BorderBrush=Brushes.Transparent;
   Cursor=Cursors.SizeAll;
   ToolTip="Drag to move. Double-click to edit.";
  }

  public TextBlock CreateLabel() {
   var output=new TextBlock {
    Text=Text,FontFamily=label.FontFamily,FontSize=label.FontSize,FontWeight=label.FontWeight,
    Foreground=label.Foreground,Background=backdrop,Padding=label.Padding,
    TextWrapping=label.TextWrapping,MaxWidth=label.MaxWidth
   };
   Canvas.SetLeft(output,Position.X+BorderThickness.Left);
   Canvas.SetTop(output,Position.Y+BorderThickness.Top);
   return output;
  }
 }
}
