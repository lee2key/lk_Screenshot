using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace LkScreenshot {
 public static class Theme {
  public const double FontSize=14.667;
  public static ResourceDictionary Load() {using(var stream=typeof(Theme).Assembly.GetManifestResourceStream("Theme.xaml"))return (ResourceDictionary)XamlReader.Load(stream);}
  public static Brush Brush(string name) {return (Brush)Application.Current.Resources[name];}
  public static Brush SelectionFill() {var brush=Brush("accent").Clone();brush.Opacity=0.2;return brush;}
  public static void Window(Window w) {w.Background=Brush("background");w.Foreground=Brush("text");w.FontFamily=new FontFamily("Segoe UI");w.FontSize=FontSize;w.UseLayoutRounding=true;w.Icon=App.WindowIcon;}
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
 }
 public class SettingsWindow : Window {
  public SettingsWindow(Preferences prefs,bool initialAuto,Action<string,bool,bool> persist) {
   Theme.Window(this);Title="lk Screenshot";Width=490;SizeToContent=SizeToContent.Height;MinWidth=420;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   var root=new DockPanel {Margin=new Thickness(12)};Content=root;
   var footer=new DockPanel {LastChildFill=false,Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var error=new TextBlock {Foreground=Theme.Brush("error"),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(error,Dock.Bottom);root.Children.Add(error);
   var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);var groups=new StackPanel();scroll.Content=groups;
   var capture=new StackPanel();capture.Children.Add(Theme.Row("Region",new TextBlock {Text="Print Screen",VerticalAlignment=VerticalAlignment.Center},180));capture.Children.Add(Theme.Row("Full screen",new TextBlock {Text="Shift + Print Screen",VerticalAlignment=VerticalAlignment.Center},180));capture.Children.Add(Theme.Row("Open Screenshots Folder",new TextBlock {Text="Ctrl + Print Screen",VerticalAlignment=VerticalAlignment.Center},180));
   var edit=new CheckBox {Content="Open editor after capture",IsChecked=prefs.Editor};Theme.FieldColumn(edit,180);capture.Children.Add(edit);
   capture.Margin=new Thickness(0,0,0,12);groups.Children.Add(capture);
   var folder=new TextBox {Text=prefs.Folder,MinWidth=80,TextAlignment=TextAlignment.Right};
   Action revealFolderEnd=delegate {folder.CaretIndex=folder.Text.Length;folder.ScrollToHorizontalOffset(double.MaxValue);};
   folder.Loaded+=delegate {revealFolderEnd();};
   folder.SizeChanged+=delegate {if(!folder.IsKeyboardFocusWithin)revealFolderEnd();};
   folder.LostKeyboardFocus+=delegate {revealFolderEnd();};
   folder.TextChanged+=delegate {if(!folder.IsKeyboardFocusWithin)revealFolderEnd();};
   var browse=App.Button("Browse",delegate {using(var picker=new System.Windows.Forms.FolderBrowserDialog()){picker.SelectedPath=folder.Text;if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)folder.Text=picker.SelectedPath;}});browse.Margin=new Thickness(12,0,0,0);
   var folderControls=new Grid();folderControls.ColumnDefinitions.Add(new ColumnDefinition());folderControls.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});folderControls.Children.Add(folder);Grid.SetColumn(browse,1);folderControls.Children.Add(browse);
   var folderRow=Theme.Row("Save to Folder",folderControls,180);((Label)folderRow.Children[0]).Target=folder;groups.Children.Add(folderRow);
   var auto=new CheckBox {Content="Start with Windows",IsChecked=initialAuto};Theme.FieldColumn(auto,180);groups.Children.Add(auto);
   var save=Theme.Primary("Save",delegate {try {persist(folder.Text,edit.IsChecked==true,auto.IsChecked==true);Close();}catch(Exception e){error.Text="Could not save settings. "+e.Message;error.Visibility=Visibility.Visible;folder.Focus();}});DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);
  }
 }
 public class TextEntryWindow : Window {
  public readonly TextBox Input=new TextBox();
  public TextEntryWindow() {
   Theme.Window(this);Title="lk Screenshot · Add text";Width=460;Height=220;MinWidth=380;MinHeight=220;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   var root=new DockPanel {Margin=new Thickness(12)};Content=root;
   var footer=new DockPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
   var add=Theme.Primary("Add",delegate {DialogResult=true;});DockPanel.SetDock(add,Dock.Right);footer.Children.Add(add);var cancel=App.Button("Cancel",delegate {DialogResult=false;});cancel.HorizontalAlignment=HorizontalAlignment.Left;footer.Children.Add(cancel);
   var row=Theme.Row("Text",Input);row.Margin=new Thickness(0);root.Children.Add(Theme.Card("Caption",row));Loaded+=delegate {Input.Focus();};
   PreviewKeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape){DialogResult=false;e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Enter){DialogResult=true;e.Handled=true;}};
  }
 }
}
