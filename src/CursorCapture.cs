using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Drawing = System.Drawing;

namespace LkScreenshot {
 internal static class CursorCapture {
  [StructLayout(LayoutKind.Sequential)]
  internal struct CursorInfo {
   public int Size, Flags;
   public IntPtr Handle;
   public Drawing.Point Position;
  }
  [StructLayout(LayoutKind.Sequential)]
  struct IconInfo {
   public int IsIcon;
   public uint HotspotX, HotspotY;
   public IntPtr Mask, Color;
  }
  [DllImport("user32.dll",SetLastError=true)] static extern bool GetCursorInfo(ref CursorInfo info);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr CopyIcon(IntPtr handle);
  [DllImport("user32.dll",SetLastError=true)] static extern bool GetIconInfo(IntPtr handle,out IconInfo info);
  [DllImport("user32.dll",SetLastError=true)] static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr handle,int width,int height,uint step,IntPtr brush,uint flags);
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);

  public static void DrawCurrent(Drawing.Graphics graphics,Drawing.Rectangle screen) {
   var info=new CursorInfo {Size=Marshal.SizeOf(typeof(CursorInfo))};
   if(!GetCursorInfo(ref info))throw new Win32Exception(Marshal.GetLastWin32Error());
   if((info.Flags&1)==0 || info.Handle==IntPtr.Zero)return;
   Draw(graphics,info.Handle,new Drawing.Point(info.Position.X-screen.Left,info.Position.Y-screen.Top));
  }

  internal static void Draw(Drawing.Graphics graphics,IntPtr cursor,Drawing.Point position) {
   IntPtr copy=CopyIcon(cursor);
   if(copy==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
   try {
    IconInfo icon;
    if(!GetIconInfo(copy,out icon))throw new Win32Exception(Marshal.GetLastWin32Error());
    try {
     IntPtr dc=graphics.GetHdc();
     try {
      // The pointer location describes its hotspot, not the top-left of the cursor image.
      if(!DrawIconEx(dc,position.X-(int)icon.HotspotX,position.Y-(int)icon.HotspotY,copy,0,0,0,IntPtr.Zero,3))
       throw new Win32Exception(Marshal.GetLastWin32Error());
     } finally {graphics.ReleaseHdc(dc);}
    } finally {
     if(icon.Mask!=IntPtr.Zero)DeleteObject(icon.Mask);
     if(icon.Color!=IntPtr.Zero)DeleteObject(icon.Color);
    }
   } finally {DestroyIcon(copy);}
  }
 }
}
