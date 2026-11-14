using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace T3Usage {
public interface IUsageUi {
 void Bind(DataCore data,T3Window tracker,T3Driver driver);
 void AttachTray(System.Windows.Forms.NotifyIcon tray);
 void TogglePopover();
 void OpenPopover();
}

// Design tokens: Apple Human Interface Dark Mode (Steve Jobs edition — precision depth, semantic restraint, zero visual noise).
public static class Theme {
 public static readonly Color ComposerColor=Color.FromRgb(0x0E,0x0E,0x0E);
 public static readonly Color PopoverColor=Color.FromRgb(0x16,0x16,0x18);
 public static readonly Color PopoverBorderOpaque=Color.FromRgb(0x36,0x36,0x3A);
 public static readonly SolidColorBrush Composer=B(0xFF0E0E0E);
 public static readonly SolidColorBrush Page=B(0xFF0A0A0C);
 public static readonly SolidColorBrush Popover=B(0xFF161618);
 public static readonly SolidColorBrush Foreground=B(0xFFF5F5F7);
 public static readonly SolidColorBrush Foreground80=B(0xD9F5F5F7);
 public static readonly SolidColorBrush Muted=B(0xFF86868B);
 public static readonly SolidColorBrush Muted70=B(0xFF6E6E73);
 public static readonly SolidColorBrush Border=B(0x16FFFFFF);
 public static readonly SolidColorBrush Separator=B(0x10FFFFFF);
 public static readonly SolidColorBrush Accent=B(0x14FFFFFF);
 public static readonly SolidColorBrush RowHighlight=B(0x18FFFFFF);
 public static readonly SolidColorBrush Input=B(0x1EFFFFFF);
 public static readonly SolidColorBrush Primary=B(0xFF0A84FF);
 public static readonly SolidColorBrush Error=B(0xFFFF453A);
 public static readonly SolidColorBrush ErrorForeground=B(0xFFFF6961);
 public static readonly SolidColorBrush ErrorSurface=B(0x26FF453A);
 public static readonly SolidColorBrush Warning=B(0xFFFF9F0A);
 public static readonly SolidColorBrush WarningForeground=B(0xFFFFB340);
 public static readonly SolidColorBrush ScrollThumb=B(0x3DFFFFFF);
 public const double TextLg=20,TextMd=15,TextSm=14,Text13=13,TextXs=12,Text2xs=11;
 public static readonly FontFamily Font=LoadFont();
 public static readonly Duration Open=new Duration(TimeSpan.FromMilliseconds(160));

 static SolidColorBrush B(uint argb){var b=new SolidColorBrush(Color.FromArgb((byte)(argb>>24),(byte)(argb>>16),(byte)(argb>>8),(byte)argb));b.Freeze();return b;}
 static FontFamily LoadFont(){
  try{string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts");
   if(File.Exists(Path.Combine(dir,"Inter-Regular.ttf")))return new FontFamily(new Uri(dir.TrimEnd('\\')+"\\"),"./#Inter, Segoe UI Variable Display, Segoe UI, Segoe UI Symbol");}
  catch(Exception e){Log.Error("Font",e);}
  return new FontFamily("Segoe UI Variable Display, Segoe UI");
 }
 public static void Crisp(DependencyObject e){
  TextOptions.SetTextFormattingMode(e,TextFormattingMode.Display);
  TextOptions.SetTextRenderingMode(e,TextRenderingMode.ClearType);
  TextOptions.SetTextHintingMode(e,TextHintingMode.Fixed);
  RenderOptions.SetClearTypeHint(e,ClearTypeHint.Enabled);
  var fe=e as FrameworkElement;if(fe!=null){fe.UseLayoutRounding=true;fe.SnapsToDevicePixels=true;}
 }
 public static TextBlock Text(string s,double size,FontWeight weight,Brush fg){
  var t=new TextBlock{Text=s??"",FontFamily=Font,FontSize=size,FontWeight=weight,Foreground=fg,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};
  System.Windows.Documents.Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);
  return t;
 }
 public static double Measure(string s,double size,FontWeight weight){
  var ft=new FormattedText(s,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(Font,FontStyles.Normal,weight,FontStretches.Normal),size,Brushes.White,null,TextFormattingMode.Display,1.0);
  return ft.WidthIncludingTrailingWhitespace;
 }
 public static void Set(TextBlock t,string text){if(t.Text!=text)t.Text=text??"";}
 public static void Set(TextBlock t,Brush b){if(!ReferenceEquals(t.Foreground,b))t.Foreground=b;}
 public static void Show(UIElement e,bool visible){var v=visible?Visibility.Visible:Visibility.Collapsed;if(e.Visibility!=v)e.Visibility=v;}
 public static Geometry Icon(string data){var g=Geometry.Parse(data);g.Freeze();return g;}
 // lucide "rotate-cw" (T3 uses lucide-react icons), 24x24 stroke 2.
 public static readonly Geometry RefreshIcon=Icon("M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8 M21 3v5h-5");
 public static System.Windows.Shapes.Path StrokeIcon(Geometry g,double size,Brush stroke){
  return new System.Windows.Shapes.Path{Data=g,Stroke=stroke,StrokeThickness=2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=size,Height=size,Stretch=Stretch.Uniform,SnapsToDevicePixels=false};
 }
}

// Apple System Semantic Quota Scale:
// >= 60% System Green (#30D158), 30-59% System Yellow (#FFD60A), 11-29% System Orange (#FF9F0A), <= 10% System Red (#FF453A).
public static class Level {
 public static readonly SolidColorBrush Green=B(0xFF30D158),Yellow=B(0xFFFFD60A),Orange=B(0xFFFF9F0A),Red=B(0xFFFF453A),Stale=B(0xFF636366),Neutral=B(0x14FFFFFF);
 static readonly SolidColorBrush[] fills={Green,Yellow,Orange,Red},tints={B(0x2230D158),B(0x22FFD60A),B(0x22FF9F0A),B(0x24FF453A)};
 static SolidColorBrush B(uint argb){var b=new SolidColorBrush(Color.FromArgb((byte)(argb>>24),(byte)(argb>>16),(byte)(argb>>8),(byte)argb));b.Freeze();return b;}
 public static int Of(int left){return left>=60?0:left>=30?1:left>=11?2:3;}
 // Text and fill colour; no number (awaiting / no data) is muted.
 public static SolidColorBrush Fill(int? left){return left.HasValue?fills[Of(left.Value)]:Theme.Muted;}
 public static SolidColorBrush Tint(int? left){return left.HasValue?tints[Of(left.Value)]:Neutral;}
 // Luminous Apple-style horizontal gradients for capsule meters.
 static readonly LinearGradientBrush[] grads={G(0xFF24B248,0xFF30D158,0xFF63E683),G(0xFFE5B800,0xFFFFD60A,0xFFFFE55C),G(0xFFE08200,0xFFFF9F0A,0xFFFFBD52),G(0xFFD92B21,0xFFFF453A,0xFFFF7970)};
 static readonly LinearGradientBrush[] quotaGrads={G(0xFF0B3D20,0xFF14532D,0xFF166534),G(0xFF574000,0xFF705300,0xFF806000),G(0xFF542700,0xFF703300,0xFF823B00),G(0xFF571515,0xFF701B1B,0xFF842020)};
 static LinearGradientBrush G(uint a,uint b,uint c){
  var g=new LinearGradientBrush{StartPoint=new Point(0,0.5),EndPoint=new Point(1,0.5),MappingMode=BrushMappingMode.RelativeToBoundingBox};
  g.GradientStops.Add(new GradientStop(B(a).Color,0));g.GradientStops.Add(new GradientStop(B(b).Color,0.7));g.GradientStops.Add(new GradientStop(B(c).Color,1));g.Freeze();return g;
 }
 public static Brush Meter(int? left,bool stale){return stale?(Brush)Stale:left.HasValue?(Brush)grads[Of(left.Value)]:Theme.Muted;}
 public static Brush QuotaMeter(int? left,bool stale){return stale?(Brush)Stale:left.HasValue?(Brush)quotaGrads[Of(left.Value)]:Theme.Muted;}
}

// UI strings and small formatting rules.
public static class UiText {
 public static string Age(DateTime? utc,DateTime now){
  if(!utc.HasValue)return "never";var d=now-utc.Value;if(d<TimeSpan.Zero)d=TimeSpan.Zero;
  if(d.TotalMinutes<1)return "just now";if(d.TotalHours<1)return (int)d.TotalMinutes+" min ago";if(d.TotalDays<1)return (int)d.TotalHours+" h ago";return (int)d.TotalDays+" d ago";
 }
 public static bool IsStale(Provider p,DateTime now){return !p.CheckedAtUtc.HasValue||now-p.CheckedAtUtc.Value>TimeSpan.FromMinutes(30);}
 public static string Runtime(string mode){
  switch(mode??""){case "full-access":return "Full access";case "approval-required":return "Supervised";case "auto-accept-edits":return "Auto-accept edits";case "":return "";}
  return Cap(mode.Replace('-',' '));
 }
 public static string Cap(string s){return string.IsNullOrEmpty(s)?"":char.ToUpperInvariant(s[0])+s.Substring(1);}
 // T3's own option text from the trigger's accessible name ("claude-opus-5.5, High · Default"); otherwise the raw options.
 public static string Options(Active a,string pickerName){
  if(a==null)return "";
  if(!string.IsNullOrEmpty(pickerName)){int i=pickerName.IndexOf(", ",StringComparison.Ordinal);if(i>0&&(pickerName.Substring(0,i)==a.ModelName||pickerName.Substring(0,i)==a.ModelSlug))return pickerName.Substring(i+2).Replace("\uFFFD","\u00B7");}
  try{var arr=SimpleJson.ParseObject("{\"o\":"+(string.IsNullOrEmpty(a.Options)?"[]":a.Options)+"}").GetArray("o");if(arr==null)return "";
   return string.Join(" \u00B7 ",arr.OfType<JsonObj>().Select(o=>Cap(o.GetString("value")??"")).Where(s=>s!="").ToArray());}catch{return "";}
 }
 public static string WindowShort(UsageWindow w){string l=w.Label??w.Id;int i=l.LastIndexOf('\u00B7');return i>=0?l.Substring(i+1).Trim():l;}
 public static string Percent(UsageResult u){return u==null||u.AwaitingRefresh?"\u2014":u.Left.Value+"%";}
 public static bool IsLow(UsageResult u){return u!=null&&u.Left.HasValue&&u.Left.Value<=10;}
 public static double Fraction(UsageResult u){return u==null||!u.Left.HasValue?-1:u.Left.Value/100.0;}
 public static string ProviderTitle(Snapshot s,string id){var p=s.Providers.FirstOrDefault(x=>x.Id==id);return p==null?id:p.Label;}
}

// Antigravity model families (Gemini, Claude & GPT shared pool, Argon); mirrors UsageMath.ModelWindows.
public sealed class Family {
 public string Key,Name;public readonly List<UsageModel> Models=new List<UsageModel>();public List<UsageWindow> Windows=new List<UsageWindow>();public UsageResult Headline;
 public static List<Family> For(Provider p,DateTime now){
  var list=new List<Family>{new Family{Key="gemini",Name="Gemini"},new Family{Key="3p",Name="Claude & GPT"},new Family{Key="argon",Name="Argon"},new Family{Key="other",Name="Other"}};
  foreach(var m in p.Models){string s=(m.Slug??"").ToLowerInvariant();string k=s.Contains("argon")?"argon":s.StartsWith("gemini")?"gemini":Regex.IsMatch(s,"^(claude|gpt)")?"3p":"other";list.First(f=>f.Key==k).Models.Add(m);}
  var result=new List<Family>();
  foreach(var f in list){
   string rep=f.Models.Count>0?f.Models[0].Slug:f.Key=="gemini"?"gemini":f.Key=="3p"?"claude":f.Key=="argon"?"gemini-argon":null;
   if(rep==null)continue;
   bool own=p.Windows.Any(w=>{string id=w.Id.ToLowerInvariant();return f.Key=="argon"?id.Contains("argon"):f.Key=="gemini"?id.StartsWith("gemini")&&!id.Contains("argon"):f.Key=="3p"?Regex.IsMatch(id,"^(3p|claude|gpt)"):false;});
   if(f.Models.Count==0&&!own)continue;
   f.Windows=UsageMath.ModelWindows(p,rep).ToList();f.Headline=UsageMath.ModelUsage(p,rep,now);result.Add(f);
  }
  return result;
 }
}

// Precision capsule meter (recessed dark track + luminous fill), drawn directly for crisp sub-pixel edges.
public sealed class Meter:FrameworkElement {
 static readonly Brush TrackBrush=MakeTrack();
 static Brush MakeTrack(){var b=new SolidColorBrush(Color.FromArgb(0x20,0xFF,0xFF,0xFF));b.Freeze();return b;}
 double fraction=-1;Brush fill=Theme.Foreground80;
 public Meter(double w,double h){Width=w;Height=h;SnapsToDevicePixels=true;VerticalAlignment=VerticalAlignment.Center;}
 public void Set(double f,Brush b){if(f==fraction&&ReferenceEquals(b,fill))return;fraction=f;fill=b;InvalidateVisual();}
 protected override void OnRender(DrawingContext dc){
  double w=ActualWidth,h=ActualHeight,r=h/2;if(w<=0||h<=0)return;
  dc.DrawRoundedRectangle(TrackBrush,null,new Rect(0,0,w,h),r,r);
  if(fraction>0){double fw=Math.Max(h,Math.Round(w*Math.Min(1,fraction)));dc.DrawRoundedRectangle(fill,null,new Rect(0,0,fw,h),r,r);}
 }
}

// 16 px Apple Watch Activity-style ring meter for the pill (radius 5.5, stroke 2, round line caps).
public sealed class Ring:FrameworkElement {
 static readonly Pen Track=MakePen(Color.FromArgb(0x38,0xFF,0xFF,0xFF));
 static Pen MakePen(Color c){var p=new Pen(new SolidColorBrush(c),2);p.Freeze();return p;}
 double fraction=-1;Brush fill=Theme.Foreground80;Pen pen;
 public Ring(){Width=16;Height=16;VerticalAlignment=VerticalAlignment.Center;}
 public void Set(double f,Brush b){if(f==fraction&&ReferenceEquals(b,fill))return;fraction=f;fill=b;pen=new Pen(b,2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();InvalidateVisual();}
 protected override void OnRender(DrawingContext dc){
  var c=new Point(8,8);const double r=5.5;dc.DrawEllipse(null,Track,c,r,r);
  if(fraction<=0||pen==null)return;
  if(fraction>=0.999){dc.DrawEllipse(null,pen,c,r,r);return;}
  double a=Math.Max(fraction,1.5/(2*Math.PI*r))*2*Math.PI;var end=new Point(c.X+r*Math.Sin(a),c.Y-r*Math.Cos(a));
  var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(c.X,c.Y-r),false,false);ctx.ArcTo(end,new Size(r,r),0,a>Math.PI,SweepDirection.Clockwise,true,false);}g.Freeze();
  dc.DrawGeometry(null,pen,g);
 }
}

// Provider glyphs: Kiro and Claude paths from T3's Icons.js, Antigravity from an optional ag_icon.png next to the exe (not shipped), otherwise a colour dot.
public static class Glyphs {
 static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>();
 public static FrameworkElement For(Provider p,double size){
  string driver=p==null?"":p.Driver;var src=Source(driver);
  if(src!=null){var img=new Image{Source=src,Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};RenderOptions.SetBitmapScalingMode(img,BitmapScalingMode.HighQuality);if(p!=null&&p.Badge!=""){var g=new Grid{Width=size,Height=size};g.Children.Add(img);var text=Theme.Text(p.Badge,7,FontWeights.SemiBold,Theme.Foreground);text.TextTrimming=TextTrimming.None;g.Children.Add(new Border{Background=Theme.Popover,CornerRadius=new CornerRadius(2),Padding=new Thickness(1,0,1,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-3,-2),Child=text});return g;}return img;}
  var color=p==null?Theme.Muted:(Brush)new BrushConverter().ConvertFromString(p.Color);if(color.CanFreeze)color.Freeze();
  var holder=new Grid{Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};holder.Children.Add(new System.Windows.Shapes.Ellipse{Width=8,Height=8,Fill=color,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});return holder;
 }
 internal static ImageSource Source(string driver){
  ImageSource s;if(cache.TryGetValue(driver,out s))return s;s=null;
  try{
   if(driver=="kiro"){var g=new DrawingGroup();g.Children.Add(new GeometryDrawing(B("#9046FF"),null,new RectangleGeometry(new Rect(0,0,1200,1200),260,260)));
    g.Children.Add(new GeometryDrawing(Brushes.White,null,Geometry.Parse("M398.554 818.914C316.315 1001.03 491.477 1046.74 620.672 940.156C658.687 1059.66 801.052 970.473 852.234 877.795C964.787 673.567 919.318 465.357 907.64 422.374C827.637 129.443 427.623 128.946 358.8 423.865C342.651 475.544 342.402 534.18 333.458 595.051C328.986 625.86 325.507 645.488 313.83 677.785C306.873 696.424 297.68 712.819 282.773 740.645C259.915 783.881 269.604 867.113 387.87 823.883L399.051 818.914H398.554Z")));
    g.Children.Add(new GeometryDrawing(Brushes.Black,null,Geometry.Parse("M636.123 549.353C603.328 549.353 598.359 510.097 598.359 486.742C598.359 465.623 602.086 448.977 609.293 438.293C615.504 428.852 624.697 424.131 636.123 424.131C647.555 424.131 657.492 428.852 664.447 438.541C672.398 449.474 676.623 466.12 676.623 486.742C676.623 525.998 661.471 549.353 636.375 549.353H636.123Z")));
    g.Children.Add(new GeometryDrawing(Brushes.Black,null,Geometry.Parse("M771.24 549.353C738.445 549.353 733.477 510.097 733.477 486.742C733.477 465.623 737.203 448.977 744.41 438.293C750.621 428.852 759.814 424.131 771.24 424.131C782.672 424.131 792.609 428.852 799.564 438.541C807.516 449.474 811.74 466.12 811.74 486.742C811.74 525.998 796.588 549.353 771.492 549.353H771.24Z")));
    s=new DrawingImage(g);}
   else if(driver=="claudeAgent"){var g=new DrawingGroup();g.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,256,257))));
    g.Children.Add(new GeometryDrawing(B("#D97757"),null,Geometry.Parse("m50.228 170.321 50.357-28.257.843-2.463-.843-1.361h-2.462l-8.426-.518-28.775-.778-24.952-1.037-24.175-1.296-6.092-1.297L0 125.796l.583-3.759 5.12-3.434 7.324.648 16.202 1.101 24.304 1.685 17.629 1.037 26.118 2.722h4.148l.583-1.685-1.426-1.037-1.101-1.037-25.147-17.045-27.22-18.017-14.258-10.37-7.713-5.25-3.888-4.925-1.685-10.758 7-7.713 9.397.649 2.398.648 9.527 7.323 20.35 15.75L94.817 91.9l3.889 3.24 1.555-1.102.195-.777-1.75-2.917-14.453-26.118-15.425-26.572-6.87-11.018-1.814-6.61c-.648-2.723-1.102-4.991-1.102-7.778l7.972-10.823L71.42 0 82.05 1.426l4.472 3.888 6.61 15.101 10.694 23.786 16.591 32.34 4.861 9.592 2.592 8.879.973 2.722h1.685v-1.556l1.36-18.211 2.528-22.36 2.463-28.776.843-8.1 4.018-9.722 7.971-5.25 6.222 2.981 5.12 7.324-.713 4.73-3.046 19.768-5.962 30.98-3.889 20.739h2.268l2.593-2.593 10.499-13.934 17.628-22.036 7.778-8.749 9.073-9.657 5.833-4.601h11.018l8.1 12.055-3.628 12.443-11.342 14.388-9.398 12.184-13.48 18.147-8.426 14.518.778 1.166 2.01-.194 30.46-6.481 16.462-2.982 19.637-3.37 8.88 4.148.971 4.213-3.5 8.62-20.998 5.184-24.628 4.926-36.682 8.685-.454.324.519.648 16.526 1.555 7.065.389h17.304l32.21 2.398 8.426 5.574 5.055 6.805-.843 5.184-12.962 6.611-17.498-4.148-40.83-9.721-14-3.5h-1.944v1.167l11.666 11.406 21.387 19.314 26.767 24.887 1.36 6.157-3.434 4.86-3.63-.518-23.526-17.693-9.073-7.972-20.545-17.304h-1.36v1.814l4.73 6.935 25.017 37.59 1.296 11.536-1.814 3.76-6.481 2.268-7.13-1.297-14.647-20.544-15.1-23.138-12.185-20.739-1.49.843-7.194 77.448-3.37 3.953-7.778 2.981-6.48-4.925-3.436-7.972 3.435-15.749 4.148-20.544 3.37-16.333 3.046-20.285 1.815-6.74-.13-.454-1.49.194-15.295 20.999-23.267 31.433-18.406 19.702-4.407 1.75-7.648-3.954.713-7.064 4.277-6.286 25.47-32.405 15.36-20.092 9.917-11.6-.065-1.686h-.583L44.07 198.125l-12.055 1.555-5.185-4.86.648-7.972 2.463-2.593 20.35-13.999-.064.065Z")));
    s=new DrawingImage(g);}
   else if(driver=="antigravity"){string f=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ag_icon.png");if(File.Exists(f)){var bi=new BitmapImage();bi.BeginInit();bi.UriSource=new Uri(f);bi.DecodePixelWidth=64;bi.CacheOption=BitmapCacheOption.OnLoad;bi.EndInit();s=bi;}}
  }catch(Exception e){Log.Error("Glyph "+driver,e);s=null;}
  if(s!=null&&s.CanFreeze)s.Freeze();cache[driver]=s;return s;
 }
 static Brush B(string hex){var b=(Brush)new BrushConverter().ConvertFromString(hex);b.Freeze();return b;}
}

public static class Native {
 [StructLayout(LayoutKind.Sequential)]public struct POINT{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]public struct RECT{public int L,T,R,B;}
 [StructLayout(LayoutKind.Sequential)]struct MONITORINFO{public int Size;public RECT Monitor,Work;public uint Flags;}
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int v,int size);
 [DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT p);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(POINT p,uint flags);
 [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr m,ref MONITORINFO mi);
 [DllImport("shcore.dll")]static extern int GetDpiForMonitor(IntPtr m,int type,out uint x,out uint y);
 [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]public static extern bool DestroyIcon(IntPtr h);
 [DllImport("user32.dll")]static extern int GetSystemMetricsForDpi(int index,uint dpi);
 [DllImport("user32.dll")]static extern uint GetDpiForSystem();
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll")]static extern bool SetProcessWorkingSetSize(IntPtr p,IntPtr min,IntPtr max);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetLong(IntPtr h,int n);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetLong(IntPtr h,int n,IntPtr value);
 public const uint NoSize=1,NoMove=2,NoZOrder=4,NoActivate=0x10,ShowWindow=0x40;
 // Windows 11: 8 px rounded corners + 1 px DWM border; returns false on systems without the attribute.
 public static bool RoundPopup(IntPtr h,Color border){
  int pref=2;bool ok=DwmSetWindowAttribute(h,33,ref pref,4)==0;
  int c=unchecked((int)0xFFFFFFFE);if(ok)ok=DwmSetWindowAttribute(h,34,ref c,4)==0;
  return ok;
 }
 public static void ToolWindow(IntPtr h){SetLong(h,-20,new IntPtr((GetLong(h,-20).ToInt64()|0x80)&~0x40000L));}
 public static Int32Rect WorkAreaAt(int x,int y,out uint dpi){var p=new POINT{X=x,Y=y};IntPtr m=MonitorFromPoint(p,2);var mi=new MONITORINFO{Size=Marshal.SizeOf(typeof(MONITORINFO))};GetMonitorInfo(m,ref mi);uint dx,dy;dpi=GetDpiForMonitor(m,0,out dx,out dy)==0?dx:96;return new Int32Rect(mi.Work.L,mi.Work.T,mi.Work.R-mi.Work.L,mi.Work.B-mi.Work.T);}
 public static Int32Rect WorkAreaForWindow(IntPtr h,out uint dpi){var m=MonitorFromWindow(h,2);var mi=new MONITORINFO{Size=Marshal.SizeOf(typeof(MONITORINFO))};GetMonitorInfo(m,ref mi);uint dx,dy;dpi=GetDpiForMonitor(m,0,out dx,out dy)==0?dx:96;return new Int32Rect(mi.Work.L,mi.Work.T,mi.Work.R-mi.Work.L,mi.Work.B-mi.Work.T);}
 public static int SmallIconSize(){try{return GetSystemMetricsForDpi(49,GetDpiForSystem());}catch{return 16;}}
 public static void TrimWorkingSet(){try{SetProcessWorkingSetSize(GetCurrentProcess(),new IntPtr(-1),new IntPtr(-1));}catch{}}
}

// Crisp tray glyph: a 1 px (2 px at 32) outlined meter filled to the remaining percentage, drawn per pixel.
public static class TrayGlyph {
 public static System.Drawing.Icon Make(int size,int? left,bool low){
  bool light=false;try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){object v=k==null?null:k.GetValue("SystemUsesLightTheme");light=v is int&&(int)v==1;}}catch{}
  var fg=light?System.Drawing.Color.FromArgb(255,26,26,26):System.Drawing.Color.FromArgb(255,245,245,245);
  var fill=low?System.Drawing.Color.FromArgb(255,251,65,74):left.HasValue&&left<=20?System.Drawing.Color.FromArgb(255,254,154,0):fg;
  int t=size>=28?2:1;int bodyH=Math.Max(6,(int)Math.Round(size*0.5));if((size-bodyH)%2!=0)bodyH++;int top=(size-bodyH)/2;
  int nub=Math.Max(1,size/16);int left0=0,right0=size-1-nub-t;
  using(var bmp=new System.Drawing.Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
   for(int x=left0;x<=right0;x++)for(int y=top;y<top+bodyH;y++){bool edge=x<left0+t||x>right0-t||y<top+t||y>=top+bodyH-t;bool corner=(x==left0||x==right0)&&(y==top||y==top+bodyH-1);if(edge&&!corner)bmp.SetPixel(x,y,fg);}
   int nubH=Math.Max(2,bodyH/3);if((bodyH-nubH)%2!=0)nubH++;for(int x=right0+1;x<=right0+nub;x++)for(int y=top+(bodyH-nubH)/2;y<top+(bodyH+nubH)/2;y++)bmp.SetPixel(x,y,fg);
   int gap=t;int ix0=left0+t+gap,ix1=right0-t-gap,iy0=top+t+gap,iy1=top+bodyH-t-gap-1;
   if(left.HasValue&&ix1>=ix0){int w=ix1-ix0+1;int fw=left.Value<=0?0:Math.Max(1,(int)Math.Round(w*left.Value/100.0,MidpointRounding.AwayFromZero));for(int x=ix0;x<ix0+fw;x++)for(int y=iy0;y<=iy1;y++)bmp.SetPixel(x,y,fill);}
   IntPtr h=bmp.GetHicon();return System.Drawing.Icon.FromHandle(h);
  }
 }
}
}
