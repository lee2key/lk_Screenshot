using System;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LkScreenshot {
 public static class ToolIcons {
  public static UIElement Create(string tool) {
   string key;
   switch(tool) {case "Arrow":key="arrow";break;case "Rectangle":key="rectangle";break;case "Pencil":key="pencil";break;case "Text":key="text";break;case "Blur":key="blur";break;case "Mosaic":key="mosaic";break;case "Crop":key="crop";break;default:throw new ArgumentException("Unknown tool", "tool");}
   var svg=new XmlDocument {XmlResolver=null};using(var stream=typeof(ToolIcons).Assembly.GetManifestResourceStream("Icons."+key+".svg"))svg.Load(stream);
   var canvas=new Canvas {Width=24,Height=24,IsHitTestVisible=false};
   bool filled=svg.DocumentElement.GetAttribute("fill")=="currentColor";
   foreach(XmlNode node in svg.DocumentElement.ChildNodes) if(node.LocalName=="path") canvas.Children.Add(new Path {Data=Geometry.Parse(node.Attributes["d"].Value),Fill=filled?Theme.Brush("text"):null,Stroke=filled?null:Theme.Brush("text"),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round});
   return new Viewbox {Width=20,Height=20,Child=canvas,IsHitTestVisible=false};
  }
 }
}
