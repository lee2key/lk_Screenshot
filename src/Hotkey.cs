using System;
using System.Runtime.Serialization;
using System.Windows.Input;

namespace LkScreenshot {
 [DataContract]
 public struct Hotkey {
  [DataMember] public Key Key { get; private set; }
  [DataMember] public ModifierKeys Modifiers { get; private set; }

  public Hotkey(Key key,ModifierKeys modifiers) : this() {
   Key=key;
   Modifiers=modifiers;
  }

  public bool IsValid {
   get {
    return KeyInterop.VirtualKeyFromKey(Key)>0 && Key!=Key.F12 &&
     Key!=Key.LeftCtrl && Key!=Key.RightCtrl && Key!=Key.LeftShift && Key!=Key.RightShift &&
     Key!=Key.LeftAlt && Key!=Key.RightAlt && Key!=Key.LWin && Key!=Key.RWin &&
     ((uint)Modifiers & ~15u)==0;
   }
  }

  public override string ToString() {
   string name=Key==Key.PrintScreen?"Print Screen":Key.ToString();
   if(Key>=Key.D0&&Key<=Key.D9)name=((int)Key-(int)Key.D0).ToString();
   else if(Key>=Key.NumPad0&&Key<=Key.NumPad9)name="Num "+((int)Key-(int)Key.NumPad0);
   return ((Modifiers&ModifierKeys.Control)!=0?"Ctrl + ":"")+
    ((Modifiers&ModifierKeys.Alt)!=0?"Alt + ":"")+
    ((Modifiers&ModifierKeys.Shift)!=0?"Shift + ":"")+
    ((Modifiers&ModifierKeys.Windows)!=0?"Win + ":"")+name;
  }

  internal static Hotkey[] Defaults() {
   return new[]{new Hotkey(Key.PrintScreen,ModifierKeys.None),
    new Hotkey(Key.PrintScreen,ModifierKeys.Shift),new Hotkey(Key.PrintScreen,ModifierKeys.Control)};
  }
 }
}
