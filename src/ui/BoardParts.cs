using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
namespace T3Usage {

// The user's arrangement of the board: provider order and collapsed model lists.
// Stored in %LOCALAPPDATA%\t3-usage\board-layout.json; T3 Code's own files are never touched for this.
public sealed class BoardLayout {
 public readonly List<string> Order=new List<string>();
 public readonly HashSet<string> Collapsed=new HashSet<string>(StringComparer.Ordinal);
 public static string FilePath{get{return Path.Combine(Log.DirectoryPath,"board-layout.json");}}
 public static BoardLayout Load(){
  var l=new BoardLayout();
  try{
   if(!File.Exists(FilePath))return l;
   var o=SimpleJson.ParseObject(File.ReadAllText(FilePath));if(o==null)return l;
   foreach(var s in (o.GetArray("order")??new List<object>()).OfType<string>())if(!l.Order.Contains(s))l.Order.Add(s);
   foreach(var s in (o.GetArray("collapsed")??new List<object>()).OfType<string>())l.Collapsed.Add(s);
  }catch(Exception e){Log.Error("Board layout",e);}
  return l;
 }
 public void Save(){
  try{
   Directory.CreateDirectory(Log.DirectoryPath);string tmp=FilePath+".tmp";
   var doc=new Dictionary<string,object>{{"order",Order.ToArray()},{"collapsed",Collapsed.OrderBy(x=>x,StringComparer.Ordinal).ToArray()}};
   File.WriteAllText(tmp,Json.Encode(doc));
   if(File.Exists(FilePath))File.Replace(tmp,FilePath,null);else File.Move(tmp,FilePath);
  }catch(Exception e){Log.Error("Board layout save",e);}
 }
 // Saved order first; providers the user never arranged follow alphabetically.
 public List<Provider> Arrange(IEnumerable<Provider> providers){
  return providers.OrderBy(p=>{int i=Order.IndexOf(p.Id);return i<0?int.MaxValue:i;})
   .ThenBy(p=>p.Label,StringComparer.CurrentCultureIgnoreCase).ThenBy(p=>p.Id,StringComparer.OrdinalIgnoreCase).ToList();
 }
 // New order of the visible providers. Hidden providers keep their places, so turning one back on returns it where it was.
 public void SetVisibleOrder(IList<string> visible){
  var full=new List<string>(Order);foreach(var id in visible)if(!full.Contains(id))full.Add(id);
  var set=new HashSet<string>(visible);int k=0;
  for(int i=0;i<full.Count;i++)if(set.Contains(full[i]))full[i]=visible[k++];
  Order.Clear();Order.AddRange(full);
 }
}

// Battery ring (Apple Batteries widget): recessed track and a round-capped arc clockwise from 12 o'clock.
public sealed class Gauge:FrameworkElement {
 readonly double thick;double fraction=-1;Pen pen;readonly Pen track;
 public Gauge(double size,double thickness){Width=size;Height=size;thick=thickness;track=new Pen(BoardStyle.Track,thickness);track.Freeze();IsHitTestVisible=false;}
 public void Set(double f,Brush b){
  fraction=f;pen=b==null?null:new Pen(b,thick){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};if(pen!=null)pen.Freeze();InvalidateVisual();
 }
 protected override void OnRender(DrawingContext dc){
  double s=Math.Min(ActualWidth,ActualHeight);if(s<=0)return;var c=new Point(ActualWidth/2,ActualHeight/2);double r=s/2-thick/2;
  dc.DrawEllipse(null,track,c,r,r);
  if(fraction<=0||pen==null)return;
  if(fraction>=0.999){dc.DrawEllipse(null,pen,c,r,r);return;}
  double a=Math.Max(fraction,0.025)*2*Math.PI;var end=new Point(c.X+r*Math.Sin(a),c.Y-r*Math.Cos(a));
  var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(c.X,c.Y-r),false,false);ctx.ArcTo(end,new Size(r,r),0,a>Math.PI,SweepDirection.Clockwise,true,false);}g.Freeze();
  dc.DrawGeometry(null,pen,g);
 }
}

// macOS-style switch, 36 x 20; the knob slides and the track fades between grey and system blue.
public sealed class SwitchView:Border {
 public const double W=36,H=20,K=16;
 readonly TranslateTransform move=new TranslateTransform();readonly SolidColorBrush fill;bool on;
 static readonly Duration Speed=new Duration(TimeSpan.FromMilliseconds(180));
 static readonly IEasingFunction Ease=MakeEase();
 static IEasingFunction MakeEase(){var e=new CubicEase{EasingMode=EasingMode.EaseOut};e.Freeze();return e;}
 public SwitchView(bool value){
  Width=W;Height=H;CornerRadius=new CornerRadius(H/2);Padding=new Thickness(2);IsHitTestVisible=false;VerticalAlignment=VerticalAlignment.Center;
  fill=new SolidColorBrush(value?BoardStyle.ActiveBorder.Color:BoardStyle.SwitchOff.Color);Background=fill;
  var knob=new Border{Width=K,Height=K,CornerRadius=new CornerRadius(K/2),Background=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,RenderTransform=move,
   Effect=new DropShadowEffect{BlurRadius=3,ShadowDepth=1,Direction=270,Opacity=0.35,Color=Colors.Black}};
  Child=knob;Set(value,false);
 }
 public bool On{get{return on;}}
 public void Set(bool value,bool animate){
  bool changed=value!=on;on=value;double x=value?W-4-K:0;var c=value?BoardStyle.ActiveBorder.Color:BoardStyle.SwitchOff.Color;
  if(animate&&changed){
   move.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(x,Speed){EasingFunction=Ease});
   fill.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(c,Speed){EasingFunction=Ease});
  }else{
   move.BeginAnimation(TranslateTransform.XProperty,null);move.X=x;fill.BeginAnimation(SolidColorBrush.ColorProperty,null);fill.Color=c;
  }
 }
}
}
