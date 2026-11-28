using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace LkScreenshot {
 internal sealed class TrayMenu : Forms.ContextMenuStrip {
  readonly Drawing.Text.PrivateFontCollection fonts=new Drawing.Text.PrivateFontCollection();
  GCHandle fontBytes;
  Drawing.Font menuFont;

  public TrayMenu() {
   var resource=Application.GetResourceStream(new Uri("pack://application:,,,/lk_Screenshot;component/fonts/robotoflex.ttf"));
   using(var stream=resource.Stream)
   using(var bytes=new MemoryStream()) {
    stream.CopyTo(bytes);
    var buffer=bytes.ToArray();
    fontBytes=GCHandle.Alloc(buffer,GCHandleType.Pinned);
    try {
     fonts.AddMemoryFont(fontBytes.AddrOfPinnedObject(),buffer.Length);
     menuFont=new Drawing.Font(fonts.Families[0],9);
    } catch {fonts.Dispose();fontBytes.Free();throw;}
   }
   Font=menuFont;
   ShowImageMargin=false;
   ForeColor=Drawing.Color.FromArgb(224,224,224);
   Padding=new Forms.Padding(5);
   Renderer=new MenuRenderer();
  }

  protected override void OnItemAdded(Forms.ToolStripItemEventArgs e) {
   base.OnItemAdded(e);
   e.Item.Padding=new Forms.Padding(12,5,14,5);
  }

  protected override void OnSizeChanged(EventArgs e) {
   base.OnSizeChanged(e);
   if(Width<2 || Height<2)return;
   using(var path=Rounded(new Drawing.Rectangle(0,0,Width,Height),Scale(9)))
    Region=new Drawing.Region(path);
  }

  int Scale(int value) {return Math.Max(1,(int)Math.Round(value*DeviceDpi/96.0));}

  static Drawing2D.GraphicsPath Rounded(Drawing.Rectangle bounds,int radius) {
   int diameter=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
   var path=new Drawing2D.GraphicsPath();
   path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
   path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
   path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
   path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);
   path.CloseFigure();
   return path;
  }

  protected override void Dispose(bool disposing) {
   base.Dispose(disposing);
   if(disposing && fontBytes.IsAllocated) {
    menuFont.Dispose();fonts.Dispose();fontBytes.Free();
   }
  }

  sealed class MenuRenderer : Forms.ToolStripRenderer {
   protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) {
    e.Graphics.Clear(Drawing.Color.FromArgb(46,46,46));
   }

   protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) {
    var bounds=new Drawing.Rectangle(0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
    if(bounds.Width<=0||bounds.Height<=0)return;
    e.Graphics.SmoothingMode=Drawing2D.SmoothingMode.AntiAlias;
    using(var pen=new Drawing.Pen(Drawing.Color.FromArgb(80,80,80)))
    using(var path=Rounded(bounds,((TrayMenu)e.ToolStrip).Scale(9)))
     e.Graphics.DrawPath(pen,path);
   }

   protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e) {
    if(!e.Item.Selected && !e.Item.Pressed)return;
    var bounds=new Drawing.Rectangle(0,0,e.Item.Width,e.Item.Height);
    e.Graphics.SmoothingMode=Drawing2D.SmoothingMode.AntiAlias;
    using(var brush=new Drawing.SolidBrush(Drawing.Color.FromArgb(71,114,179)))
    using(var path=Rounded(bounds,((TrayMenu)e.ToolStrip).Scale(5)))
     e.Graphics.FillPath(brush,path);
   }

   protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e) {
    using(var brush=new Drawing.SolidBrush(e.Item.ForeColor))
    using(var format=new Drawing.StringFormat {LineAlignment=Drawing.StringAlignment.Center}) {
     e.Graphics.DrawString(e.Text,e.TextFont,brush,e.TextRectangle,format);
    }
   }
  }
 }
}
