using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace LkScreenshot {
 public static class Theme {
  public const double FontSize=14.667;
  public static ResourceDictionary Load() {using(var stream=typeof(Theme).Assembly.GetManifestResourceStream("Theme.xaml"))return (ResourceDictionary)XamlReader.Load(stream);}
  public static Brush Brush(string name) {return (Brush)Application.Current.Resources[name];}
  public static Brush SelectionFill() {var brush=Brush("accent").Clone();brush.Opacity=0.2;return brush;}
  internal static readonly FontFamily InterfaceFont=new FontFamily(new Uri("pack://application:,,,/"),"./Fonts/#Roboto Flex");
  public static void Window(Window w) {w.Background=Brush("background");w.Foreground=Brush("text");w.FontFamily=InterfaceFont;w.FontSize=FontSize;w.UseLayoutRounding=true;w.Icon=App.WindowIcon;}
  public static Border Card(string title,UIElement content) {
   var panel=new DockPanel();if(!String.IsNullOrEmpty(title)){var label=new TextBlock {Text=title,FontWeight=FontWeights.SemiBold,Margin=new Thickness(4,0,4,12)};DockPanel.SetDock(label,Dock.Top);panel.Children.Add(label);}panel.Children.Add(content);
   return new Border {Background=Brush("surface"),BorderBrush=Brush("surface-border"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(12,10,12,12),Child=panel};
  }
  public static Grid Row(string title,FrameworkElement control,double labelWidth=120) {
   var row=new Grid {MinHeight=26,Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(labelWidth)});row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(12)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var label=new Label {Content=title,Target=control,HorizontalContentAlignment=HorizontalAlignment.Right,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(0)};row.Children.Add(label);Grid.SetColumn(control,2);row.Children.Add(control);return row;
  }
  public static void FieldColumn(FrameworkElement control,double labelWidth=120) {control.Margin=new Thickness(labelWidth+12,0,0,0);}
  public static Button Primary(string label,Action action) {var b=App.Button(label,action);b.Tag="primary";return b;}
  public static Button SaveButton(Action action) {
   var button=Primary("Save",action);
   var content=new StackPanel {Orientation=Orientation.Horizontal};
   content.Children.Add(new System.Windows.Shapes.Path {
    Width=14,Height=14,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,7,0),
    Stroke=Brush("text"),StrokeThickness=1.4,StrokeLineJoin=PenLineJoin.Round,
    StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,
    Data=Geometry.Parse("M 2.1,1.4 H 10.5 L 12.6,3.5 V 12.6 H 2.1 Z M 4.9,1.4 V 5.32 H 9.52 V 1.4 M 4.62,12.6 V 8.4 H 10.08 V 12.6")
   });
   content.Children.Add(new TextBlock {Text="Save",VerticalAlignment=VerticalAlignment.Center});
   button.Content=content;
   System.Windows.Automation.AutomationProperties.SetName(button,"Save");
   return button;
  }
 }
 public class SettingsWindow : Window {
  public SettingsWindow(Preferences prefs,bool initialAuto,Action<string,bool,bool,Hotkey[]> persist) {
   Theme.Window(this);Title="lk_Screenshot";Width=490;SizeToContent=SizeToContent.Height;MinWidth=420;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   var root=new DockPanel {Margin=new Thickness(12)};Content=root;
   var footer=new DockPanel {LastChildFill=false,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var error=new TextBlock {Foreground=Theme.Brush("error"),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(error,Dock.Bottom);root.Children.Add(error);
   var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);var groups=new StackPanel();scroll.Content=groups;
   var capture=new StackPanel();
   var shortcuts=(Hotkey[])prefs.Hotkeys.Clone();
   var actions=new[]{"Region","Full screen","Open Screenshots Folder"};
   for(int i=0;i<actions.Length;i++)AddShortcutField(capture,actions[i],shortcuts,i);
   var edit=new CheckBox {Content="Open Editor After Capture",IsChecked=prefs.Editor};Theme.FieldColumn(edit,180);capture.Children.Add(edit);
   capture.Margin=new Thickness(0,0,0,12);groups.Children.Add(capture);
   var folder=new TextBox {Text=prefs.Folder,MinWidth=80};
   var browse=App.Button("Browse",delegate {using(var picker=new System.Windows.Forms.FolderBrowserDialog()){picker.SelectedPath=folder.Text;if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)folder.Text=picker.SelectedPath;}});browse.Margin=new Thickness(6,0,0,0);
   var folderControls=new Grid();folderControls.ColumnDefinitions.Add(new ColumnDefinition());folderControls.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});folderControls.Children.Add(folder);Grid.SetColumn(browse,1);folderControls.Children.Add(browse);
   var folderRow=Theme.Row("Save to Folder",folderControls,180);((Label)folderRow.Children[0]).Target=folder;groups.Children.Add(folderRow);
   var auto=new CheckBox {Content="Start with Windows",IsChecked=initialAuto};Theme.FieldColumn(auto,180);groups.Children.Add(auto);
   var save=Theme.SaveButton(delegate {try {persist(folder.Text,edit.IsChecked==true,auto.IsChecked==true,shortcuts);Close();}catch(Exception e){error.Text="Could not save settings. "+e.Message;error.Visibility=Visibility.Visible;}});DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);
  }
  static void AddShortcutField(Panel panel,string label,Hotkey[] shortcuts,int index) {
   bool recording=false;
   var button=new Button {Content=shortcuts[index].ToString(),ToolTip="Click to record a shortcut. Esc cancels."};
   System.Windows.Automation.AutomationProperties.SetName(button,label+" shortcut");
   Action stop=delegate {recording=false;button.Content=shortcuts[index].ToString();};
   button.Click+=delegate {recording=true;button.Content="Press a shortcut…";button.Focus();};
   button.LostKeyboardFocus+=delegate {stop();};
   KeyEventHandler record=delegate(object sender,KeyEventArgs e) {
    if(!recording)return;
    var key=e.Key==Key.System?e.SystemKey:e.Key;
    if(e.IsDown && key==Key.Tab){stop();return;}
    if(e.IsUp && key!=Key.PrintScreen)return;
    e.Handled=true;
    if(key==Key.Escape){stop();return;}
    if(e.IsRepeat)return;
    var shortcut=new Hotkey(key,Keyboard.Modifiers);
    if(!shortcut.IsValid)return;
    shortcuts[index]=shortcut;stop();
   };
   button.PreviewKeyDown+=record;
   button.PreviewKeyUp+=record;
   panel.Children.Add(Theme.Row(label,button,180));
  }
 }
}
