using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LkScreenshot {
 internal sealed class ImageDocument {
  public WriteableBitmap Bitmap { get; private set; }
  readonly LinkedList<Action> history = new LinkedList<Action>();
  public int UndoCount { get { return history.Count; } }

  public ImageDocument(BitmapSource source) {
   Bitmap = new WriteableBitmap(source.Format == PixelFormats.Bgra32 ? source :
       new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0));
  }

  internal static Int32Rect PixelBounds(Rect area, int width, int height) {
   area.Intersect(new Rect(0, 0, width, height));
   if (area.IsEmpty || area.Width <= 0 || area.Height <= 0) return Int32Rect.Empty;
   int left = (int)Math.Floor(area.Left), top = (int)Math.Floor(area.Top);
   return new Int32Rect(left, top, (int)Math.Ceiling(area.Right) - left, (int)Math.Ceiling(area.Bottom) - top);
  }

  internal void Remember(Action restore) {
   history.AddLast(restore);
   if (history.Count > 12) history.RemoveFirst();
  }

  public void Undo() {
   if (history.Count == 0) return;
   history.Last.Value();
   history.RemoveLast();
  }

  internal static byte[] ReadPixels(BitmapSource image, Int32Rect area) {
   int stride = checked(area.Width * 4);
   var pixels = new byte[checked(stride * area.Height)];
   image.CopyPixels(area, pixels, stride, 0);
   return pixels;
  }

  void Apply(Int32Rect area, byte[] before, byte[] after) {
   int stride = area.Width * 4;
   Bitmap.WritePixels(area, after, stride, 0);
   Remember(delegate { Bitmap.WritePixels(area, before, stride, 0); });
  }

  public void Crop(Int32Rect area,Action restoreOverlay=null) {
   if (area.IsEmpty || (area.Width == Bitmap.PixelWidth && area.Height == Bitmap.PixelHeight)) return;
   var previous = Bitmap;
   Bitmap = new WriteableBitmap(new CroppedBitmap(previous, area));
   Remember(delegate { Bitmap = previous;if(restoreOverlay!=null)restoreOverlay(); });
  }

  public void Draw(UIElement mark) {
   // Lay out only the annotation in image pixels, independently of the editor's Viewbox and monitor DPI.
   var layer = new Canvas();
   layer.Children.Add(mark);
   var size = new Size(Bitmap.PixelWidth, Bitmap.PixelHeight);
   layer.Measure(size);
   layer.Arrange(new Rect(size));
   var bounds = VisualTreeHelper.GetDescendantBounds(layer);
   if (bounds.IsEmpty) return;
   bounds.Inflate(1, 1);
   var area = PixelBounds(bounds, Bitmap.PixelWidth, Bitmap.PixelHeight);
   if (area.IsEmpty) return;
   var root = new ContainerVisual();
   var positioned = new ContainerVisual { Offset = new Vector(-area.X, -area.Y) };
   positioned.Children.Add(layer);
   root.Children.Add(positioned);
   var raster = new RenderTargetBitmap(area.Width, area.Height, 96, 96, PixelFormats.Pbgra32);
   raster.Render(root);
   var paint = ReadPixels(raster, new Int32Rect(0, 0, area.Width, area.Height));
   var before = ReadPixels(Bitmap, area);
   var after = (byte[])before.Clone();
   bool changed = false;
   for (int i = 0; i < paint.Length; i += 4) {
    int coverage = paint[i + 3];
    if (coverage == 0) continue;
    changed = true;
    int background = before[i + 3] * (255 - coverage);
    int alpha = coverage * 255 + background;
    for (int c = 0; c < 3; c++)
     after[i + c] = (byte)Math.Min(255, (paint[i + c] * 65025 + before[i + c] * background + alpha / 2) / alpha);
    after[i + 3] = (byte)((alpha + 127) / 255);
   }
   if (changed) Apply(area, before, after);
  }

  public void Mosaic(Int32Rect area) {
   if(area.IsEmpty)return;
   var before=ReadPixels(Bitmap,area);
   var after=(byte[])before.Clone();
   const int blockSize=12;
   for(int top=0;top<area.Height;top+=blockSize)
    for(int left=0;left<area.Width;left+=blockSize) {
     int right=Math.Min(left+blockSize,area.Width),bottom=Math.Min(top+blockSize,area.Height);
     long blue=0,green=0,red=0,alpha=0;
     for(int y=top;y<bottom;y++)for(int x=left;x<right;x++) {
      int i=(y*area.Width+x)*4,a=before[i+3];
      blue+=before[i]*a;green+=before[i+1]*a;red+=before[i+2]*a;alpha+=a;
     }
     if(alpha==0)continue;
     for(int y=top;y<bottom;y++)for(int x=left;x<right;x++) {
      int i=(y*area.Width+x)*4;
      after[i]=(byte)(blue/alpha);after[i+1]=(byte)(green/alpha);after[i+2]=(byte)(red/alpha);
     }
    }
   Apply(area,before,after);
  }

  public void Blur(Int32Rect area) {
   if (area.IsEmpty) return;
   var before = ReadPixels(Bitmap, area);
   var horizontal = new byte[before.Length];
   var after = (byte[])before.Clone();
   int stride = area.Width * 4;
   for (int y = 0; y < area.Height; y++)
    BlurLine(before, horizontal, y * stride, 4, area.Width);
   for (int x = 0; x < area.Width; x++)
    BlurLine(horizontal, after, x * 4, stride, area.Height);
   Apply(area, before, after);
  }

  static void BlurLine(byte[] source, byte[] destination, int start, int step, int length) {
   const int radius = 12;
   for (int channel = 0; channel < 3; channel++) {
    int sum = 0, left = 0, right = 0;
    int offset = start + channel;
    for (int i = 0; i < length; i++) {
     while (right < length && right <= i + radius) sum += source[offset + right++ * step];
     while (left < i - radius) sum -= source[offset + left++ * step];
     destination[offset + i * step] = (byte)(sum / (right - left));
    }
   }
  }
 }
}
