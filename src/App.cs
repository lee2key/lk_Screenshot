using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Markup;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace LkScreenshot {
 [DataContract] public class Preferences {
  [DataMember] public string Folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Screenshots");
  [DataMember] public bool Editor = true;
  [DataMember] public Hotkey[] Hotkeys = Hotkey.Defaults();
  public static string FilePath { get { return SettingsPath(App.Name); } }
  static string SettingsPath(string name) {
   return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),name,"settings.json");
  }
  static string ReadPath {get {return File.Exists(FilePath)?FilePath:SettingsPath(App.LegacyId);}}
  public static bool Exists {get {return File.Exists(ReadPath);}}
  [OnDeserializing] void SetDefaults(StreamingContext context) {Editor=true;Hotkeys=Hotkey.Defaults();}
  public static string FullFolder(string folder) {
   string path=Environment.ExpandEnvironmentVariables((folder??"").Trim()).Replace('/','\\');
   string root=System.IO.Path.GetPathRoot(path);
   if(string.IsNullOrEmpty(root)||root=="\\"||root.EndsWith(":"))throw new ArgumentException("Enter the full folder path.");
   return System.IO.Path.GetFullPath(path);
  }
  public static Preferences Load() {
   try {using(var f=File.OpenRead(ReadPath)) {
    var prefs=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(f) ?? new Preferences();
    try {prefs.Folder=FullFolder(prefs.Folder);}catch(ArgumentException){prefs.Folder=new Preferences().Folder;}
    if(prefs.Hotkeys==null || prefs.Hotkeys.Length!=3 || Array.Exists(prefs.Hotkeys,key=>!key.IsValid))
     prefs.Hotkeys=Hotkey.Defaults();
    return prefs;
   }} catch {return new Preferences();}
  }
  public void Save() {
   Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath));
   App.WriteFile(FilePath,delegate(Stream stream){new DataContractJsonSerializer(typeof(Preferences)).WriteObject(stream,this);});
  }
 }
 public static class Native {
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 }
 public class App : Application {
  internal const string LegacyId="lkScreenshot";
  const string PreviousName="lk Screenshot";
  public const string Name="lk_Screenshot";
  Forms.NotifyIcon tray; Drawing.Icon trayIcon; TrayMenu trayMenu; HwndSource source; Preferences prefs; Window active; bool busy; Mutex mutex; bool testHotkeys;
  static BitmapSource windowIcon;
  public static BitmapSource WindowIcon {get {if(windowIcon==null){using(var stream=typeof(App).Assembly.GetManifestResourceStream("AppIcon.ico")){var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);windowIcon=decoder.Frames[decoder.Frames.Count-1];windowIcon.Freeze();}}return windowIcon;}}
  static Drawing.Icon LoadTrayIcon() {using(var stream=typeof(App).Assembly.GetManifestResourceStream("AppIcon.ico"))using(var icon=new Drawing.Icon(stream,Forms.SystemInformation.SmallIconSize))return (Drawing.Icon)icon.Clone();}
  public static Brush Accent { get { return Theme.Brush("accent"); } }
  [STAThread] public static void Main(string[] args) {
   try { RunMain(args); } catch(Exception e) {
    var log=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lk_Screenshot-error.txt");File.WriteAllText(log,e.ToString());MessageBox.Show("Could not start lk_Screenshot. Details: "+log+"\n"+e.Message);Environment.ExitCode=1;
   }
  }
  static void RunMain(string[] args) {
   Native.SetProcessDPIAware();
   var app=new App(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
   app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
    var log=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lk_Screenshot-error.txt");
    try {File.AppendAllText(log,DateTime.Now+"\n"+e.Exception+"\n\n");}catch{}
    if(app.active is EditorWindow) ((EditorWindow)app.active).CancelGesture();
    MessageBox.Show("The operation failed. The editor remains open.\n"+e.Exception.Message+"\n\nDetails: "+log,"lk_Screenshot");e.Handled=true;
   };
   app.testHotkeys=Array.IndexOf(args,"--test-hotkeys")>=0;
   app.Resources=Theme.Load();
   if(args.Length>0 && args[0]=="--self-test") {
    var folder=args.Length>1?args[1]:"test-output";
    try { SelfTest.Run(folder); Environment.ExitCode=0; }
    catch(Exception e) {Environment.ExitCode=1;try {File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"FAIL\n"+e);}catch {Console.Error.WriteLine(e);} }
    return;
   }
   if(args.Length>0 && args[0]=="--editor-demo") {app.Startup+=delegate {var editor=new EditorWindow(SelfTest.Sample(),delegate(BitmapSource b){WritePng(b,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"demo-result.png"));return true;});app.active=editor;editor.Closed+=delegate {app.Shutdown();};editor.Show();};app.Run();return;}
   bool first; app.mutex=new Mutex(true,"Local\\"+LegacyId+".Prototype",out first); if(!first) { MessageBox.Show("lk_Screenshot is already running. Open Settings from the system tray."); app.mutex.Dispose(); return; }
   app.Startup+=delegate { app.Start(); }; app.Exit+=delegate { app.Cleanup(); }; app.Run(); app.mutex.Dispose();
  }
  public static Button Button(string text, Action action) { var b=new Button {Content=text}; b.Click+=delegate { action(); }; return b; }
  public static TextBlock Label(string text,double size) { return new TextBlock {Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center}; }
  void Start() {
   prefs=Preferences.Load();
   source=new HwndSource(new HwndSourceParameters("lk_Screenshot hotkeys") {Width=0,Height=0,WindowStyle=0}); source.AddHook(Hook);
   trayIcon=LoadTrayIcon();tray=new Forms.NotifyIcon {Text=Name,Icon=trayIcon,Visible=true};
   trayMenu=new TrayMenu();
   trayMenu.Items.Add("Settings",null,delegate { Settings(); });
   trayMenu.Items.Add("Exit",null,delegate { Shutdown(); }); tray.ContextMenuStrip=trayMenu; tray.DoubleClick+=delegate { Settings(); };
   RestoreHotkeys();
   if(!Preferences.Exists) Settings();
  }
  List<Hotkey> RegisterHotkeys(Hotkey[] shortcuts) {
   var unavailable=new List<Hotkey>();
   var modifiers=ModifierKeys.Control|ModifierKeys.Alt|ModifierKeys.Shift;
   var testKeys=new[]{Key.D7,Key.D8,Key.D9};
   for(int i=0;i<shortcuts.Length;i++) {
    var shortcut=testHotkeys?new Hotkey(testKeys[i],modifiers):shortcuts[i];
    if(!Native.RegisterHotKey(source.Handle,i+1,(uint)shortcut.Modifiers|0x4000u,(uint)KeyInterop.VirtualKeyFromKey(shortcut.Key)))
     unavailable.Add(shortcut);
   }
   return unavailable;
  }
  void UnregisterHotkeys() {
   for(int i=1;i<=3;i++)Native.UnregisterHotKey(source.Handle,i);
  }
  void RestoreHotkeys() {
   var unavailable=RegisterHotkeys(prefs.Hotkeys);
   if(unavailable.Count>0)MessageBox.Show("Could not register "+string.Join(", ",unavailable)+". Choose different shortcuts in Settings.","Shortcuts unavailable");
  }
  IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) { if(msg==0x312) {int id=w.ToInt32();if(id==3){handled=true;OpenFolder();}else if(id==1||id==2){handled=true;Capture(id==2);}} return IntPtr.Zero; }
  void OpenFolder() {try {var folder=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(prefs.Folder));Directory.CreateDirectory(folder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {FileName=folder,UseShellExecute=true});}catch(Exception e){MessageBox.Show("Could not open the screenshots folder.\n"+e.Message,Name);}}
  void Cleanup() { if(tray!=null) {tray.Visible=false;tray.Dispose();}if(trayMenu!=null)trayMenu.Dispose();if(trayIcon!=null)trayIcon.Dispose(); if(source!=null) {Native.UnregisterHotKey(source.Handle,1);Native.UnregisterHotKey(source.Handle,2);Native.UnregisterHotKey(source.Handle,3);source.Dispose();} }
  public static BitmapSource Grab() {
   var r=Forms.Screen.PrimaryScreen.Bounds;
   using(var b=new Drawing.Bitmap(r.Width,r.Height,Drawing.Imaging.PixelFormat.Format32bppRgb)) {
    using(var g=Drawing.Graphics.FromImage(b)) {
     g.CopyFromScreen(r.Left,r.Top,0,0,r.Size,Drawing.CopyPixelOperation.SourceCopy);
     CursorCapture.DrawCurrent(g,r);
    }
    return FromCapture(b);
   }
  }
  internal static BitmapSource FromCapture(Drawing.Bitmap capture) {
   var rect=new Drawing.Rectangle(0,0,capture.Width,capture.Height);
   var data=capture.LockBits(rect,Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppRgb);
   try {
    var image=BitmapSource.Create(rect.Width,rect.Height,96,96,PixelFormats.Bgr32,null,data.Scan0,checked(data.Stride*rect.Height),data.Stride);
    image.Freeze();return image;
   } finally {capture.UnlockBits(data);}
  }
  void Capture(bool full) {
   if(busy || active!=null) return; busy=true;
   var capturedAt=DateTime.Now;
   // Let a tray menu disappear before sampling the desktop.
   var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(160)};
   timer.Tick+=delegate {
    timer.Stop();
    try {
     var image=Grab();
     if(full) Process(image,capturedAt);
     else {
      var select=new SelectionWindow(image);
      select.Closed+=delegate {active=null;busy=false;if(select.Result!=null)Process(select.Result,capturedAt);};
      select.Show();active=select;
     }
    } catch(Exception e) {busy=false;MessageBox.Show(e.Message,"Capture failed");}
   };timer.Start();
  }
  void Process(BitmapSource image,DateTime capturedAt) {
   busy=true;
   try {
    if(!prefs.Editor && SaveImage(image,capturedAt))return;
    var editor=new EditorWindow(image,edited=>SaveImage(edited,capturedAt));editor.Closed+=delegate {active=null;busy=false;};
    editor.Show();active=editor;
   } finally {busy=active!=null;}
  }
  bool SaveImage(BitmapSource image,DateTime capturedAt) {
   string path=null;
   string stem=ScreenshotStem(capturedAt);
   string filename=stem+".png";
   try {Directory.CreateDirectory(prefs.Folder);path=SaveUniqueScreenshot(image,prefs.Folder,stem);}
   catch(Exception e) {
    if(MessageBox.Show("Could not save the screenshot.\n"+e.Message+"\n\nChoose another location?","Save failed",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return false;
    var dialog=new SaveFileDialog {FileName=filename,DefaultExt=".png",Filter="PNG image (*.png)|*.png"};
    if(dialog.ShowDialog()!=true)return false;
    path=dialog.FileName;
    try {WritePng(image,path);}catch(Exception error){MessageBox.Show(error.Message,"Save failed");return false;}
   }
   while(true) {
    try {Clipboard.SetImage(image);return true;}
    catch(COMException) {
     if(MessageBox.Show("File saved:\n"+path+"\n\nThe clipboard is busy. Retry copying?","Clipboard unavailable",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return true;
    }
   }
  }
  internal static string ScreenshotStem(DateTime capturedAt) {
   return "Screenshot_"+capturedAt.ToString("yyyy-MM-dd_HH-mm",System.Globalization.CultureInfo.InvariantCulture);
  }
  internal static string SaveUniqueScreenshot(BitmapSource image,string folder,string stem) {
   for(int index=1;;index++) {
    string name=stem+(index==1?"":"_"+index.ToString(System.Globalization.CultureInfo.InvariantCulture))+".png";
    string path=System.IO.Path.Combine(folder,name);
    if(File.Exists(path))continue;
    try {WritePng(image,path,false);return path;}
    catch(IOException) {if(!File.Exists(path))throw;}
   }
  }
  internal static void WriteFile(string path,Action<Stream> write,bool overwrite=true) {
   string temp=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)),System.IO.Path.GetRandomFileName());
   var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None);
   try {
    using(file)write(file);
    if(overwrite && File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
   } catch {File.Delete(temp);throw;}
  }
  public static void WritePng(BitmapSource image,string path,bool overwrite=true) {
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));WriteFile(path,png.Save,overwrite);
  }
  void Settings() {
   if(active!=null) {active.Activate();return;}
   if(busy)return;
   bool initialAuto=!Preferences.Exists||AutostartEnabled();
   bool saved=false;
   var w=new SettingsWindow(prefs,initialAuto,delegate(string folder,bool editor,bool auto,Hotkey[] shortcuts) {
    var target=Preferences.FullFolder(folder);
    if(new HashSet<Hotkey>(shortcuts).Count!=shortcuts.Length)throw new ArgumentException("Assign a different shortcut to each action.");
    try {
     var unavailable=RegisterHotkeys(shortcuts);
     if(unavailable.Count>0)throw new InvalidOperationException("Shortcut unavailable: "+string.Join(", ",unavailable)+".");
     Directory.CreateDirectory(target);
     bool previousAuto=AutostartEnabled();
     if(auto||previousAuto)SetAutostart(auto);
     var updated=new Preferences {Folder=target,Editor=editor,Hotkeys=shortcuts};
     try {updated.Save();}catch {if(auto!=previousAuto)SetAutostart(previousAuto);throw;}
     prefs=updated;saved=true;
    } catch {UnregisterHotkeys();throw;}
   });
   UnregisterHotkeys();
   w.Closed+=delegate {active=null;if(!saved)RestoreHotkeys();};
   try {w.Show();active=w;}catch {RestoreHotkeys();throw;}
  }
  static bool AutostartEnabled() {try {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key!=null && (key.GetValue(Name)!=null || key.GetValue(PreviousName)!=null || key.GetValue(LegacyId)!=null);}catch(System.Security.SecurityException){return false;}catch(UnauthorizedAccessException){return false;}}
  static void SetAutostart(bool enabled) {using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {if(enabled) key.SetValue(Name,"\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\"");else key.DeleteValue(Name,false);key.DeleteValue(PreviousName,false);key.DeleteValue(LegacyId,false);}}
 }
 public class SelectionWindow : Window {
  readonly Canvas overlay=new Canvas(); Point start; bool dragging; Rectangle selection; public BitmapSource Result;
  public SelectionWindow(BitmapSource image) {
   FontFamily=Theme.InterfaceFont;
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Topmost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;
   var screen=Forms.Screen.PrimaryScreen.Bounds;double scale;using(var g=Drawing.Graphics.FromHwnd(IntPtr.Zero)) scale=g.DpiX/96.0;
   // The captured bitmap and overlay use the same stretched coordinate space.
   Left=screen.Left/scale;Top=screen.Top/scale;Width=screen.Width/scale;Height=screen.Height/scale;
   var grid=new Grid();Content=grid;grid.Children.Add(new Image {Source=image,Stretch=Stretch.Fill});grid.Children.Add(new Border {Background=new SolidColorBrush(Color.FromArgb(100,0,0,0))});grid.Children.Add(overlay);
   overlay.Background=Brushes.Transparent;
   var hint=App.Label("Drag to select a region  ·  Esc to cancel",Theme.FontSize);hint.Foreground=Theme.Brush("text");var hintBorder=new Border {Child=hint,Background=Theme.Brush("surface"),CornerRadius=new CornerRadius(4),Padding=new Thickness(12),IsHitTestVisible=false};Canvas.SetLeft(hintBorder,12);Canvas.SetTop(hintBorder,12);overlay.Children.Add(hintBorder);
   overlay.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e) {if(!overlay.CaptureMouse())return;start=e.GetPosition(overlay);dragging=true;selection=new Rectangle {Stroke=App.Accent,StrokeThickness=2,Fill=Theme.SelectionFill()};overlay.Children.Add(selection);};
   overlay.LostMouseCapture+=delegate {if(dragging){dragging=false;overlay.Children.Remove(selection);}};
   overlay.MouseMove+=delegate(object s,MouseEventArgs e) {if(dragging) {var r=Box(start,e.GetPosition(overlay));Canvas.SetLeft(selection,r.X);Canvas.SetTop(selection,r.Y);selection.Width=r.Width;selection.Height=r.Height;}};
   overlay.MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e) {if(!dragging)return;dragging=false;overlay.ReleaseMouseCapture();var r=Box(start,e.GetPosition(overlay));r.Intersect(new Rect(overlay.RenderSize));if(r.Width<2||r.Height<2) {overlay.Children.Remove(selection);return;}Result=Crop(image,r,overlay.ActualWidth,overlay.ActualHeight);Close();};
   KeyDown+=delegate(object s,KeyEventArgs e) {if(e.Key==Key.Escape) Close();};
  }
  public static Rect Box(Point a,Point b) {return new Rect(new Point(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Point(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));}
  public static BitmapSource Crop(BitmapSource image,Rect r,double width,double height) {
   r.Scale(image.PixelWidth/width,image.PixelHeight/height);
   var crop=new CroppedBitmap(image,ImageDocument.PixelBounds(r,image.PixelWidth,image.PixelHeight));crop.Freeze();return crop;
  }
 }
 public class EditorWindow : Window {
  readonly ImageDocument document;
  CropOverlay cropOverlay;
  BitmapSource bitmap { get {return document.Bitmap;} }
  readonly Canvas canvas=new Canvas {Background=Brushes.Transparent,ClipToBounds=true,UseLayoutRounding=false,Focusable=true};
  readonly Image picture=new Image {Stretch=Stretch.Fill,IsHitTestVisible=false};
  string tool="Pencil"; Brush ink=Brushes.OrangeRed; Point start; bool drawing; UIElement preview; Polyline freehand; Func<BitmapSource,bool> save; Dictionary<string,Button> tools=new Dictionary<string,Button>();
  InlineText inlineText;
  string textBeforeEdit;
  Point textPositionBeforeEdit;
  readonly Canvas textLayer=new Canvas();
  readonly Dictionary<Color,Button> colorButtons=new Dictionary<Color,Button>();
  readonly Grid imageHost=new Grid {ClipToBounds=true};
  readonly Viewbox imageView=new Viewbox {Stretch=Stretch.Uniform,StretchDirection=StretchDirection.Both};
  readonly ScaleTransform zoomScale=new ScaleTransform();
  readonly TranslateTransform zoomOffset=new TranslateTransform();
  public EditorWindow(BitmapSource image,Func<BitmapSource,bool> save) {
   Theme.Window(this);
   document=new ImageDocument(image);this.save=save;Title="lk_Screenshot · Editor";
   var work=SystemParameters.WorkArea;Width=Math.Min(1100,work.Width);Height=Math.Min(760,work.Height);MinWidth=Math.Min(620,Width);MinHeight=Math.Min(360,Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
   var root=new DockPanel {Margin=new Thickness(12),Background=Background};TextElement.SetForeground(root,Foreground);Content=root;
   var footer=new DockPanel {LastChildFill=false,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var done=Theme.SaveButton(Finish);done.ToolTip="Save and copy · Ctrl+C / Ctrl+S";DockPanel.SetDock(done,Dock.Right);footer.Children.Add(done);
   var bar=new WrapPanel();
   foreach(var name in new[]{"Pencil","Rectangle","Arrow","Text","Blur","Mosaic","Crop"}) {string t=name;var b=App.Button("",delegate {CancelGesture();FinishTextEdit();ApplyCrop();tool=t;if(tool=="Crop")BeginCrop();UpdateTools();});b.ToolTip=t=="Text"?"Double-click to add or edit text. Drag to move.":t;b.Content=ToolIcons.Create(t);System.Windows.Automation.AutomationProperties.SetName(b,t);b.Width=34;b.Height=32;b.Padding=new Thickness(6);b.Margin=new Thickness(0,0,6,0);tools.Add(t,b);bar.Children.Add(b);}
   var colors=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(6,0,0,0)};bar.Children.Add(colors);
   foreach(var color in new[]{Colors.OrangeRed,Colors.Orange,Colors.Gold,Colors.LimeGreen,Colors.Turquoise,Colors.DodgerBlue,Colors.MediumPurple,Colors.HotPink,Colors.White,Colors.Black}) {
    var c=color;var b=App.Button("",delegate {SelectColor(c);});b.Width=32;b.Height=32;b.Padding=new Thickness(4);b.Margin=new Thickness(0,0,4,0);
    b.Content=new Ellipse {Width=16,Height=16,Fill=new SolidColorBrush(c),Stroke=Theme.Brush("text"),StrokeThickness=1};System.Windows.Automation.AutomationProperties.SetName(b,"Color "+c);colorButtons.Add(c,b);colors.Children.Add(b);
   }
   var toolCard=Theme.Card(null,bar);toolCard.Padding=new Thickness(12,10,12,10);toolCard.Margin=new Thickness(0,0,0,12);DockPanel.SetDock(toolCard,Dock.Top);root.Children.Add(toolCard);
   RenderOptions.SetBitmapScalingMode(picture,BitmapScalingMode.HighQuality);
   canvas.Children.Add(picture);canvas.Children.Add(textLayer);imageView.Child=canvas;imageHost.Children.Add(imageView);
   var zoom=new TransformGroup();zoom.Children.Add(zoomScale);zoom.Children.Add(zoomOffset);
   imageView.RenderTransform=zoom;
   imageHost.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e) {
    e.Handled=true;
    if(!drawing && !imageHost.IsMouseCaptureWithin)ZoomAt(e.GetPosition(imageHost),e.Delta);
   };
   imageView.SizeChanged+=delegate {if(zoomScale.ScaleX>1)ClampZoom();UpdateCropScale();};
   root.Children.Add(Theme.Card(null,imageHost));
   Refresh();UpdateTools();SelectColor(Colors.OrangeRed);
   canvas.MouseLeftButtonDown+=Down;canvas.MouseMove+=Move;canvas.MouseLeftButtonUp+=Up;
   canvas.LostMouseCapture+=delegate {if(drawing)CancelGesture();};
   PreviewKeyDown+=EditorKeyDown;
  }
  void EditorKeyDown(object sender,KeyEventArgs e) {
   bool control=Keyboard.Modifiers==ModifierKeys.Control;
   if(e.Key==Key.Escape) {
    if(inlineText!=null)CancelTextEdit();else if(cropOverlay!=null)CancelCrop();else Close();
   } else if(inlineText!=null && inlineText.Input.IsKeyboardFocusWithin && !(control && e.Key==Key.S)) {
    return;
   } else if(control&&(e.Key==Key.C||e.Key==Key.S))Finish();
   else if(e.Key==Key.Z && control)Undo();
   else if(e.Key!=Key.Enter)return;
   e.Handled=true;
  }
  void UpdateTools() {textLayer.IsHitTestVisible=tool=="Text";canvas.Cursor=tool=="Text"?Cursors.IBeam:null;foreach(var pair in tools) {pair.Value.Tag=pair.Key==tool?"selected":null;System.Windows.Automation.AutomationProperties.SetHelpText(pair.Value,pair.Key==tool?"Selected tool":"");}}
  void SelectColor(Color color) {
   ink=new SolidColorBrush(color);
   foreach(var pair in colorButtons) {bool selected=pair.Key==color;pair.Value.Tag=selected?"selected":null;pair.Value.BorderBrush=selected?Theme.Brush("button-focus"):Brushes.Transparent;System.Windows.Automation.AutomationProperties.SetHelpText(pair.Value,selected?"Current color":"");}
  }
  void Refresh() {
   if(canvas.Width!=bitmap.PixelWidth || canvas.Height!=bitmap.PixelHeight)ResetZoom();
   canvas.Width=textLayer.Width=picture.Width=bitmap.PixelWidth;canvas.Height=textLayer.Height=picture.Height=bitmap.PixelHeight;
   picture.Source=bitmap;
  }
  void ResetZoom() {
   zoomScale.ScaleX=zoomScale.ScaleY=1;zoomOffset.X=zoomOffset.Y=0;
  }
  void ZoomAt(Point pointer,int delta) {
   double scale=zoomScale.ScaleX,next=Math.Max(1,Math.Min(8,scale*Math.Pow(1.2,delta/120.0)));
   if(next==scale)return;
   if(next==1)ResetZoom();
   else {
    Point origin=imageView.TranslatePoint(new Point(),imageHost);
    double ratio=next/scale;
    zoomOffset.X+=(pointer.X-origin.X)*(1-ratio);
    zoomOffset.Y+=(pointer.Y-origin.Y)*(1-ratio);
    zoomScale.ScaleX=zoomScale.ScaleY=next;
    ClampZoom();
   }
   UpdateCropScale();
  }
  void ClampZoom() {
   var bounds=canvas.TransformToAncestor(imageHost).TransformBounds(new Rect(canvas.RenderSize));
   zoomOffset.X+=ViewportCorrection(bounds.Left,bounds.Width,imageHost.ActualWidth);
   zoomOffset.Y+=ViewportCorrection(bounds.Top,bounds.Height,imageHost.ActualHeight);
  }
  static double ViewportCorrection(double start,double size,double viewport) {
   if(size<=viewport)return (viewport-size)/2-start;
   return Math.Max(viewport-size,Math.Min(0,start))-start;
  }
  Point Position(MouseEventArgs e) {var p=e.GetPosition(canvas);return new Point(Math.Max(0,Math.Min(canvas.Width,p.X)),Math.Max(0,Math.Min(canvas.Height,p.Y)));}
  void Down(object sender,MouseButtonEventArgs e) {
   e.Handled=true;
   if(cropOverlay!=null)return;
   start=Position(e);
   if(tool=="Text") {
    if(inlineText!=null&&inlineText.IsMouseOver)return;
    FinishTextEdit();
    if(e.ClickCount==2)BeginText(start);
    return;
   }
   BeginGesture(start);if(!canvas.CaptureMouse())CancelGesture();
  }
  void BeginGesture(Point p) {
   CancelGesture();start=p;
   if(tool=="Pencil") {freehand=new Polyline {IsHitTestVisible=false,Stroke=ink,StrokeThickness=3,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};freehand.Points.Add(start);preview=freehand;canvas.Children.Add(preview);}
   drawing=true;
  }
  void Move(object sender,MouseEventArgs e) {
   UpdateGesture(Position(e));
  }
  void UpdateGesture(Point p) {
   if(!drawing)return;
   if(tool=="Pencil") {if(freehand.Points[freehand.Points.Count-1]!=p)freehand.Points.Add(p);return;}
   if(preview==null) {
    preview=tool=="Arrow"?(UIElement)Arrow(start,p,ink):new Rectangle {Stroke=tool=="Rectangle"?ink:App.Accent,StrokeThickness=3,Fill=tool=="Rectangle"?Brushes.Transparent:Theme.SelectionFill()};
    preview.IsHitTestVisible=false;canvas.Children.Add(preview);if(tool=="Arrow")return;
   }
   if(tool=="Arrow") {((System.Windows.Shapes.Path)preview).Data=ArrowGeometry(start,p);return;}
   var r=SelectionWindow.Box(start,p);var box=(Rectangle)preview;box.Width=r.Width;box.Height=r.Height;Canvas.SetLeft(box,r.X);Canvas.SetTop(box,r.Y);
  }
  void Up(object sender,MouseButtonEventArgs e) {
   e.Handled=true;EndGesture(Position(e));
  }
  void EndGesture(Point p) {
   if(!drawing)return;UpdateGesture(p);drawing=false;canvas.ReleaseMouseCapture();var r=SelectionWindow.Box(start,p);
   var mark=preview;canvas.Children.Remove(mark);preview=null;
   if(tool=="Pencil" && freehand.Points.Count==1) {
    mark=new Ellipse {Width=3,Height=3,Fill=ink};Canvas.SetLeft(mark,p.X-1.5);Canvas.SetTop(mark,p.Y-1.5);
   }
   freehand=null;
   if(tool=="Crop"||tool=="Blur"||tool=="Mosaic") {
    if(r.Width<2||r.Height<2)return;
    var area=ImageDocument.PixelBounds(r,bitmap.PixelWidth,bitmap.PixelHeight);
    if(tool=="Crop")CropImage(area);else if(tool=="Mosaic")document.Mosaic(area);else document.Blur(area);
   } else document.Draw(mark);
  }
  public void CancelGesture() {drawing=false;canvas.ReleaseMouseCapture();if(preview!=null)canvas.Children.Remove(preview);preview=null;freehand=null;}
  void BeginText(Point point) {
   var text=new InlineText(textLayer,point,ink,FontFamily,EditText,delegate {FinishTextEdit();});
   text.Moved+=delegate(Point oldPosition) {document.Remember(delegate {text.Position=oldPosition;});};
   textLayer.Children.Add(text);
   EditText(text);
   textBeforeEdit=null;
  }
  void EditText(InlineText text) {
   FinishTextEdit();
   inlineText=text;textBeforeEdit=text.Text;textPositionBeforeEdit=text.Position;
   text.StartTyping();
  }
  void CancelTextEdit() {
   var text=inlineText;
   inlineText=null;
   if(textBeforeEdit==null)textLayer.Children.Remove(text);
   else {text.Text=textBeforeEdit;text.Position=textPositionBeforeEdit;text.FinishTyping();}
   Keyboard.Focus(canvas);
  }
  void FinishTextEdit() {
   if(inlineText==null)return;
   var text=inlineText;
   var previous=textBeforeEdit;
   var oldPosition=textPositionBeforeEdit;
   inlineText=null;
   text.FinishTyping();
   if(string.IsNullOrWhiteSpace(text.Text)) {
    int index=textLayer.Children.IndexOf(text);
    textLayer.Children.RemoveAt(index);
    if(previous!=null)document.Remember(delegate {text.Text=previous;text.Position=oldPosition;textLayer.Children.Insert(index,text);});
   } else if(previous==null)document.Remember(delegate {textLayer.Children.Remove(text);});
   else if(previous!=text.Text || oldPosition!=text.Position)
    document.Remember(delegate {text.Text=previous;text.Position=oldPosition;});
   Keyboard.Focus(canvas);
  }
  BitmapSource ComposeScreenshot() {
   if(textLayer.Children.Count==0)return bitmap;
   var output=new ImageDocument(bitmap);
   foreach(InlineText text in textLayer.Children)output.Draw(text.CreateLabel());
   return output.Bitmap;
  }
  void Undo() {if(inlineText!=null){CancelTextEdit();return;}if(cropOverlay!=null){CancelCrop();return;}if(drawing||imageHost.IsMouseCaptureWithin||document.UndoCount==0)return;document.Undo();Refresh();}
  void Finish() {if(drawing||imageHost.IsMouseCaptureWithin)return;FinishTextEdit();ApplyCrop();if(save(ComposeScreenshot()))Close();}
  void BeginCrop() {
   cropOverlay=new CropOverlay(bitmap.PixelWidth,bitmap.PixelHeight);
   canvas.Children.Add(cropOverlay);UpdateCropScale();
  }
  void UpdateCropScale() {if(cropOverlay!=null)cropOverlay.SetScale(zoomScale.ScaleX*imageView.ActualWidth/bitmap.PixelWidth);}
  void RemoveCropOverlay() {
   canvas.Children.Remove(cropOverlay);cropOverlay=null;
  }
  void ApplyCrop() {
   if(cropOverlay==null)return;
   var area=ImageDocument.PixelBounds(cropOverlay.Area,bitmap.PixelWidth,bitmap.PixelHeight);
   RemoveCropOverlay();CropImage(area);
  }
  void CropImage(Int32Rect area) {
   if(area.IsEmpty || (area.Width==bitmap.PixelWidth && area.Height==bitmap.PixelHeight))return;
   var positions=new Dictionary<InlineText,Point>();
   foreach(InlineText text in textLayer.Children)positions.Add(text,text.Position);
   document.Crop(area,delegate {foreach(var entry in positions)entry.Key.Position=entry.Value;});
   foreach(var entry in positions)entry.Key.Position=entry.Value-new Vector(area.X,area.Y);
   Refresh();
  }
  void CancelCrop() {RemoveCropOverlay();tool="Pencil";UpdateTools();}
  internal void VerifyFit() {
   var bounds=canvas.TransformToAncestor(imageHost).TransformBounds(new Rect(0,0,canvas.Width,canvas.Height));
   if(bounds.Width<=0||bounds.Height<=0||bounds.Left< -1||bounds.Top< -1||bounds.Right>imageHost.ActualWidth+1||bounds.Bottom>imageHost.ActualHeight+1)throw new Exception("Image is clipped by the editor viewport");
   if(Math.Abs(bounds.Width/bounds.Height-(double)bitmap.PixelWidth/bitmap.PixelHeight)>0.01)throw new Exception("Image aspect ratio changed");
   var point=new Point(canvas.Width*0.75,canvas.Height*0.25);var screenPoint=canvas.TransformToAncestor(imageHost).Transform(point);var restored=imageHost.TransformToDescendant(canvas).Transform(screenPoint);
   if((point-restored).Length>0.01)throw new Exception("Scaled drawing coordinates");
  }
  internal void VerifyZoom() {
   var center=new Point(imageHost.ActualWidth/2,imageHost.ActualHeight/2);
   ZoomAt(center,600);
   var anchor=new Point(center.X+35,center.Y+20);
   var imagePoint=imageHost.TranslatePoint(anchor,canvas);
   ZoomAt(anchor,120);
   if(zoomScale.ScaleX<=2 || (anchor-canvas.TranslatePoint(imagePoint,imageHost)).Length>0.1)
    throw new Exception("Zoom must preserve the image point under the pointer");
   var area=new Int32Rect((int)imagePoint.X-3,(int)imagePoint.Y-3,7,7);
   var pixels=ImageDocument.ReadPixels(bitmap,area);
   tool="Pencil";
   var point=imageHost.TranslatePoint(anchor,canvas);
   BeginGesture(point);EndGesture(point);
   SelfTest.CheckEdit(pixels,ImageDocument.ReadPixels(bitmap,area),area.Width,new Int32Rect(0,0,7,7));
   Undo();
   SelfTest.Equal(pixels,ImageDocument.ReadPixels(bitmap,area),"Zoomed edit undo");
   ZoomAt(anchor,-2400);VerifyFit();
  }
  internal void VerifyLargeEdit() {
   int right=bitmap.PixelWidth-1,bottom=bitmap.PixelHeight-1;
   var untouched=ImageDocument.ReadPixels(bitmap,new Int32Rect(10,10,128,64));
   var corner=new Int32Rect(right-150,bottom-40,151,41);
   var before=ImageDocument.ReadPixels(bitmap,corner);
   tool="Pencil";BeginGesture(new Point(right-120,bottom-17));EndGesture(new Point(right-5,bottom-17));
   var edited=ImageDocument.ReadPixels(bitmap,corner);
   SelfTest.CheckEdit(before,edited,151,new Int32Rect(28,21,120,5));
   int middle=(23*151+80)*4;
   if(edited[middle]!=0 || edited[middle+1]!=69 || edited[middle+2]!=255 || edited[middle+3]!=255)throw new Exception("4K pencil position/color");
   SelfTest.Equal(untouched,ImageDocument.ReadPixels(bitmap,new Int32Rect(10,10,128,64)),"4K untouched region");
   Undo();SelfTest.Equal(before,ImageDocument.ReadPixels(bitmap,corner),"4K undo");
  }
  internal void VerifyTools(string folder) {
   colorButtons[Colors.Gold].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   int selectedColors=0;foreach(var pair in colorButtons)if((string)pair.Value.Tag=="selected")selectedColors++;
   if(((SolidColorBrush)ink).Color!=Colors.Gold||selectedColors!=1||(string)colorButtons[Colors.Gold].Tag!="selected")throw new Exception("Current color selection");
   colorButtons[Colors.OrangeRed].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   var original=SelfTest.Pixels(bitmap);
   foreach(var name in new[]{"Arrow","Rectangle","Pencil","Blur","Mosaic","Crop"}) {
    tool=name;BeginGesture(new Point(25,25));UpdateGesture(new Point(80,55));UpdateGesture(new Point(110,85));EndGesture(new Point(150,120));
    var edited=SelfTest.Pixels(bitmap);
    if(name=="Crop") {
     if(bitmap.PixelWidth!=125||bitmap.PixelHeight!=95)throw new Exception("Editor crop size");
     SelfTest.CompareRegion(original,320,new Int32Rect(25,25,125,95),edited);
    } else {
     var region=(name=="Blur"||name=="Mosaic")?new Int32Rect(25,25,125,95):new Int32Rect(21,21,133,103);
     SelfTest.CheckEdit(original,edited,320,region);
     if(name=="Rectangle"){
      var interior=new Int32Rect(30,30,115,85);
      SelfTest.CompareRegion(original,320,interior,ImageDocument.ReadPixels(bitmap,interior));
     }
    }
    App.WritePng(bitmap,System.IO.Path.Combine(folder,"tool-"+name+".png"));Undo();SelfTest.Equal(original,SelfTest.Pixels(bitmap),name+" undo");
    BeginGesture(new Point(30,30));UpdateGesture(new Point(90,90));CancelGesture();
    if(drawing||preview!=null||freehand!=null||document.UndoCount!=0)throw new Exception(name+": cancel");
    SelfTest.Equal(original,SelfTest.Pixels(bitmap),name+" cancel");
   }
   tools["Crop"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   if(cropOverlay==null)throw new Exception("Crop tool did not open handles");
   SelfTest.RenderCropPreview(this,folder);
   cropOverlay.Resize(0,1,25,0);cropOverlay.Resize(1,0,0,30);
   cropOverlay.Resize(2,2,-145,-75);ApplyCrop();
   if(bitmap.PixelWidth!=150||bitmap.PixelHeight!=95)throw new Exception("Crop handles size");
   SelfTest.CompareRegion(original,320,new Int32Rect(25,30,150,95),SelfTest.Pixels(bitmap));
   Undo();SelfTest.Equal(original,SelfTest.Pixels(bitmap),"Crop handles undo");
   tools["Crop"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));cropOverlay.Resize(0,0,50,50);CancelCrop();
   SelfTest.Equal(original,SelfTest.Pixels(bitmap),"Cancel crop handles");
   tools["Text"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   BeginText(new Point(10,10));
   var firstText=inlineText;
   firstText.Text="Sample\ntext";
   FinishTextEdit();
   BeginText(new Point(185,100));
   var secondText=inlineText;
   secondText.Text="Another";
   FinishTextEdit();
   firstText.MoveTo(new Point(60,40));
   EditText(firstText);
   if(!firstText.Editing || firstText.Text!="Sample\ntext" || !firstText.Input.AcceptsReturn)
    throw new Exception("Reopen multiline text");
   firstText.Text="Edited\ntext";
   FinishTextEdit();
   SelfTest.RenderTextPreview(this,folder);
   if(!firstText.IsHitTestVisible || firstText.Background==null ||
    VisualTreeHelper.HitTest(firstText,new Point(5,5))==null)
    throw new Exception("Finished text must receive mouse input");
   SelfTest.Equal(original,SelfTest.Pixels(bitmap),"Text remains editable before export");
   var composed=ComposeScreenshot();
   SelfTest.CheckEdit(original,SelfTest.Pixels(composed),320,new Int32Rect(60,40,245,105));
   App.WritePng(composed,System.IO.Path.Combine(folder,"tool-text.png"));
   Undo();if(firstText.Text!="Sample\ntext")throw new Exception("Undo text edit");
   Undo();Undo();if(textLayer.Children.Count!=0)throw new Exception("Undo text creation");
   SelfTest.Equal(original,SelfTest.Pixels(bitmap),"Text undo");

   tool="Pencil";var states=new List<byte[]> {original};
   for(int i=0;i<20;i++){
    int y=5+9*i;
    BeginGesture(new Point(7,y));UpdateGesture(new Point(81.25,y+0.5));EndGesture(new Point(121,y));
    var after=SelfTest.Pixels(bitmap);
    SelfTest.CheckEdit(states[i],after,320,new Int32Rect(5,y-2,119,5));
    states.Add(after);
   }
   if(document.UndoCount!=12)throw new Exception("Undo limit");
   var path=System.IO.Path.Combine(folder,"annotation.png");
   App.WritePng(bitmap,path);SelfTest.Equal(states[20],SelfTest.ReadPng(path),"PNG after 20 strokes");
   for(int i=19;i>=8;i--){Undo();SelfTest.Equal(states[i],SelfTest.Pixels(bitmap),"Stroke undo");}

   tool="Pencil";BeginGesture(new Point(319,199));EndGesture(new Point(319,199));
   var dotted=SelfTest.Pixels(bitmap);
   SelfTest.CheckEdit(states[8],dotted,320,new Int32Rect(317,197,3,3));
   tool="Crop";BeginGesture(new Point(320,200));EndGesture(new Point(170.5,110.5));
   SelfTest.CompareRegion(dotted,320,new Int32Rect(170,110,150,90),SelfTest.Pixels(bitmap));
   tool="Blur";var cropped=SelfTest.Pixels(bitmap);
   BeginGesture(new Point(150,90));EndGesture(new Point(100,50));
   SelfTest.CheckEdit(cropped,SelfTest.Pixels(bitmap),150,new Int32Rect(100,50,50,40));
   Undo();SelfTest.Equal(cropped,SelfTest.Pixels(bitmap),"Blur after crop undo");
   Undo();SelfTest.Equal(dotted,SelfTest.Pixels(bitmap),"Crop after pencil undo");
   Undo();SelfTest.Equal(states[8],SelfTest.Pixels(bitmap),"Edge dot undo");
  }
  public static UIElement Arrow(Point a,Point b,Brush ink) {
   return new System.Windows.Shapes.Path {Data=ArrowGeometry(a,b),Stroke=ink,StrokeThickness=6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
  }
  static Geometry ArrowGeometry(Point a,Point b) {
   var g=new GeometryGroup();g.Children.Add(new LineGeometry(a,b));var v=a-b;if(v.Length>0){v.Normalize();var n=new Vector(-v.Y,v.X);g.Children.Add(new LineGeometry(b,b+v*15+n*7));g.Children.Add(new LineGeometry(b,b+v*15-n*7));}return g;
  }
 }
 public static class SelfTest {
  internal static byte[] Pixels(BitmapSource image) {
   if(image.Format!=PixelFormats.Bgra32)image=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
   return ImageDocument.ReadPixels(image,new Int32Rect(0,0,image.PixelWidth,image.PixelHeight));
  }
  internal static byte[] ReadPng(string path) {
   using(var stream=File.OpenRead(path))return Pixels(new PngBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0]);
  }
  internal static void Equal(byte[] expected,byte[] actual,string operation) {
   if(expected.Length!=actual.Length)throw new Exception(operation+": incorrect size");
   for(int i=0;i<actual.Length;i++)if(expected[i]!=actual[i])throw new Exception(operation+": mismatch at byte "+i);
  }
  internal static void CompareRegion(byte[] original,int width,Int32Rect area,byte[] actual) {
   var expected=new byte[area.Width*area.Height*4];
   for(int y=0;y<area.Height;y++)Buffer.BlockCopy(original,((area.Y+y)*width+area.X)*4,expected,y*area.Width*4,area.Width*4);
   Equal(expected,actual,"Crop pixels");
  }
  internal static void CheckEdit(byte[] before,byte[] after,int width,Int32Rect region) {
   if(before.Length!=after.Length)throw new Exception("Edit changed dimensions");
   bool changed=false;
   for(int i=0;i<after.Length;i++)if(before[i]!=after[i]) {
    int x=i/4%width,y=i/4/width;
    if(x<region.X||x>=region.X+region.Width||y<region.Y||y>=region.Y+region.Height)throw new Exception("Unexpected change at "+x+", "+y);
    changed=true;
   }
   if(!changed)throw new Exception("Edit did not change any pixels");
  }
  internal static void RenderCropPreview(Window window,string folder) {
   RenderWindow(window,817,457,System.IO.Path.Combine(folder,"crop-handles.png"),1);
  }
  internal static void RenderTextPreview(Window window,string folder) {
   RenderWindow(window,817,457,System.IO.Path.Combine(folder,"text-preview.png"),1);
  }
  static void RenderWindow(Window window,double width,double height,string path,double scale) {
   var content=(FrameworkElement)window.Content;var panel=content as Panel;if(panel!=null)panel.Background=Theme.Brush("background");
   if(height<=0){content.Measure(new Size(width,double.PositiveInfinity));height=Math.Ceiling(content.DesiredSize.Height);}
   content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
   var preview=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);preview.Render(content);App.WritePng(preview,path);
  }
  public static BitmapSource Sample() {var pixels=new byte[640*360*4];for(int y=0;y<360;y++)for(int x=0;x<640;x++){int i=(y*640+x)*4;pixels[i]=(byte)(35+x/5);pixels[i+1]=(byte)(35+y/3);pixels[i+2]=(byte)(40+(x%32)*4);pixels[i+3]=255;}return BitmapSource.Create(640,360,96,96,PixelFormats.Bgra32,null,pixels,640*4);}
  static void VerifyCursor(string folder) {
   foreach(var cursor in new[]{Forms.Cursors.Arrow,Forms.Cursors.Cross,Forms.Cursors.IBeam,Forms.Cursors.Hand}) {
    using(var large=new Drawing.Bitmap(192,192,Drawing.Imaging.PixelFormat.Format32bppRgb))
    using(var clipped=new Drawing.Bitmap(64,64,Drawing.Imaging.PixelFormat.Format32bppRgb)) {
     using(var g=Drawing.Graphics.FromImage(large)){g.Clear(Drawing.Color.CornflowerBlue);CursorCapture.Draw(g,cursor.Handle,new Drawing.Point(96,96));}
     using(var g=Drawing.Graphics.FromImage(clipped)){g.Clear(Drawing.Color.CornflowerBlue);CursorCapture.Draw(g,cursor.Handle,new Drawing.Point(2,2));}
     var image=App.FromCapture(large);var original=Pixels(image);
     int left=192,top=192,right=0,bottom=0;
     for(int y=0;y<192;y++)for(int x=0;x<192;x++) {
      int i=(y*192+x)*4;
      if(original[i]==237&&original[i+1]==149&&original[i+2]==100)continue;
      left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
     }
     if(left>right)throw new Exception("Cursor missing: "+cursor);
     if(cursor==Forms.Cursors.Cross && (Math.Abs((left+right)/2.0-96)>2 || Math.Abs((top+bottom)/2.0-96)>2))
      throw new Exception("Cursor hotspot alignment");
     CompareRegion(original,192,new Int32Rect(94,94,64,64),Pixels(App.FromCapture(clipped)));
     string path=System.IO.Path.Combine(folder,"cursor-"+cursor.ToString().Replace("[Cursor: ","").Replace("]","")+".png");
     App.WritePng(image,path);Equal(original,ReadPng(path),"Cursor PNG");
    }
   }
  }
  static void VerifyCaptureAndSave(string folder) {
   BitmapSource capture;
   using(var native=new Drawing.Bitmap(3,2,Drawing.Imaging.PixelFormat.Format32bppRgb)) {
    native.SetPixel(0,0,Drawing.Color.Red);native.SetPixel(2,1,Drawing.Color.Blue);
    capture=App.FromCapture(native);
   }
   byte[] expected={0,0,255,255, 0,0,0,255, 0,0,0,255, 0,0,0,255, 0,0,0,255, 255,0,0,255};
   Equal(expected,Pixels(capture),"Capture orientation, color and opacity");
   Equal(expected,Pixels(new ImageDocument(capture).Bitmap),"Open native capture in editor");
   string path=System.IO.Path.Combine(folder,"capture-"+Guid.NewGuid().ToString("N")+".png");
   try {
    App.WritePng(capture,path,false);Equal(expected,ReadPng(path),"Unedited capture PNG");
    try {App.WriteFile(path,delegate(Stream f){f.WriteByte(0);throw new IOException("test");});throw new Exception("Write failure was lost");} catch(IOException){}
    Equal(expected,ReadPng(path),"Existing file after interrupted write");
    try {App.WritePng(Sample(),path,false);throw new Exception("Duplicate file overwritten");} catch(IOException){}
    Equal(expected,ReadPng(path),"Filename collision");
   } finally {File.Delete(path);}
  }
  public static void Run(string folder) {
   GlyphTypeface glyph;
   var typeface=new Typeface(Theme.InterfaceFont,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
   if(!typeface.TryGetGlyphTypeface(out glyph) || !glyph.FontUri.ToString().Contains("robotoflex.ttf"))
    throw new Exception("Bundled Roboto Flex font was not selected");
   using(var trayMenu=new TrayMenu()) {
    if(trayMenu.Font.FontFamily.Name!="Roboto Flex")throw new Exception("Bundled tray font was not selected");
   }
   var serializer=new DataContractJsonSerializer(typeof(Preferences));
   using(var old=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Editor\":false}"))) {
    var preferences=(Preferences)serializer.ReadObject(old);
    if(!preferences.Hotkeys[0].Equals(Hotkey.Defaults()[0])||preferences.Editor)
     throw new Exception("Legacy settings shortcuts");
    var shortcut=new Hotkey(Key.F9,ModifierKeys.Control|ModifierKeys.Shift);
    preferences.Hotkeys[0]=shortcut;
    using(var stream=new MemoryStream()) {
     serializer.WriteObject(stream,preferences);stream.Position=0;
     if(!shortcut.Equals(((Preferences)serializer.ReadObject(stream)).Hotkeys[0]))
      throw new Exception("Custom shortcut roundtrip");
    }
   }
   Directory.CreateDirectory(folder);var pixels=new byte[320*200*4];for(int y=0;y<200;y++)for(int x=0;x<320;x++){int i=(y*320+x)*4;pixels[i]=(byte)(x%256);pixels[i+1]=(byte)y;pixels[i+2]=(byte)((x%2)*255);pixels[i+3]=255;}
   VerifyCaptureAndSave(folder);
   VerifyCursor(folder);
   var namingFolder=System.IO.Path.Combine(folder,"names-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(namingFolder);
   try {
    var capturedAt=new DateTime(2026,11,28,2,15,58);
    string stem=App.ScreenshotStem(capturedAt);
    if(stem!="Screenshot_2026-11-28_02-15")throw new Exception("Capture timestamp in screenshot name");
    var first=App.SaveUniqueScreenshot(Sample(),namingFolder,stem);
    var second=App.SaveUniqueScreenshot(Sample(),namingFolder,stem);
    if(System.IO.Path.GetFileName(first)!=stem+".png" || System.IO.Path.GetFileName(second)!=stem+"_2.png")
     throw new Exception("Screenshot filenames");
   } finally {
    foreach(var file in Directory.GetFiles(namingFolder))File.Delete(file);
    Directory.Delete(namingFolder);
   }
   var image=BitmapSource.Create(320,200,144,144,PixelFormats.Bgra32,null,pixels,320*4);var crop=SelectionWindow.Crop(image,new Rect(20,30,100,80),320,200);if(crop.PixelWidth!=100||crop.PixelHeight!=80)throw new Exception("Crop size");
   var mosaicDoc=new ImageDocument(image);var mosaicArea=new Int32Rect(30,30,25,25);
   mosaicDoc.Mosaic(mosaicArea);var mosaic=Pixels(mosaicDoc.Bitmap);
   CheckEdit(pixels,mosaic,320,mosaicArea);
   for(int y=30;y<42;y++)for(int x=30;x<42;x++)for(int c=0;c<3;c++)
    if(mosaic[(y*320+x)*4+c]!=mosaic[(30*320+30)*4+c])throw new Exception("Mosaic block is not uniform");
   mosaicDoc.Undo();Equal(pixels,Pixels(mosaicDoc.Bitmap),"Mosaic exact undo");
   var blurDoc=new ImageDocument(image);blurDoc.Blur(new Int32Rect(30,30,100,80));
   var blurred=Pixels(blurDoc.Bitmap);CheckEdit(pixels,blurred,320,new Int32Rect(30,30,100,80));
   if(blurred[(50*320+50)*4+2]!=122)throw new Exception("Box blur average");
   App.WritePng(crop,System.IO.Path.Combine(folder,"crop.png"));App.WritePng(blurDoc.Bitmap,System.IO.Path.Combine(folder,"blur.png"));
   var clipped=SelectionWindow.Crop(image,new Rect(-10,-5,20.2,12.4),160,100);
   CompareRegion(pixels,320,new Int32Rect(0,0,21,15),Pixels(clipped));
   var editor=new EditorWindow(image,delegate {return true;});
   RenderWindow(editor,817,457,System.IO.Path.Combine(folder,"editor-preview.png"),1.5);
   editor.VerifyTools(folder);editor.VerifyFit();editor.VerifyZoom();editor.Close();
   var largeImage=new TransformedBitmap(Sample(),new ScaleTransform(6,6));var largeEditor=new EditorWindow(largeImage,delegate {return true;});RenderWindow(largeEditor,1060,680,System.IO.Path.Combine(folder,"editor-large.png"),1);largeEditor.VerifyFit();RenderWindow(largeEditor,820,400,System.IO.Path.Combine(folder,"editor-small.png"),1.5);largeEditor.VerifyFit();largeEditor.VerifyLargeEdit();largeEditor.Close();
   var portraitImage=new TransformedBitmap(largeImage,new RotateTransform(90));var portraitEditor=new EditorWindow(portraitImage,delegate {return true;});RenderWindow(portraitEditor,820,400,System.IO.Path.Combine(folder,"editor-portrait.png"),1);portraitEditor.VerifyFit();portraitEditor.Close();
   var settings=new SettingsWindow(new Preferences {Folder=@"C:\Users\USERNAME\Documents\Screenshots"},false,delegate{});RenderWindow(settings,466,0,System.IO.Path.Combine(folder,"settings-preview.png"),1);RenderWindow(settings,466,0,System.IO.Path.Combine(folder,"settings-150.png"),1.5);settings.Close();
   File.WriteAllText(System.IO.Path.Combine(folder,"result.txt"),"PASS: minute-based filenames and collision suffix; inline text input, placement, commit and undo; custom shortcut roundtrip and legacy defaults; cursor-centered zoom and zoomed edit undo; cursor arrow, cross, I-beam and hand rendering, hotspot alignment, edge clipping and PNG roundtrip; mosaic block uniformity, boundaries and undo; crop side/corner handles, apply and cancel; capture RGB, orientation and opacity; unedited PNG; interrupted write and filename collision preserve previous file; crop; blur boundaries; PNG roundtrip; SVG tool rendering; current color changes and highlights exactly one swatch; editor layout; arrow, rectangle, pencil, blur, crop gestures; text commit; undo each tool; cancel each gesture; 20 consecutive pencil strokes; 12-action pixel-exact undo; untouched pixels and prior strokes byte-identical after every edit; PNG pixels after 20 strokes; cropped boundary and edge dot; full image fits before/after resizing; landscape 3840x2160 and portrait 2160x3840; 150% render and 144 DPI source; 4K edge stroke position, color and undo; scaled drawing coordinates.");
  }
 }
}
