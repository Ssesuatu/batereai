using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
namespace T3Usage {

// Tokens of the usage board: Apple HIG Dark Mode layered surfaces, specular highlights, and semantic accents.
public static class BoardStyle {
 public static readonly SolidColorBrush Dim=B(0xFF636366),Ok=B(0xFF30D158),Card=B(0xFF1C1C1E),CardElevated=B(0xFF1E2026),CardEdge=B(0x17FFFFFF),
  SidebarSurface=B(0xFF111113),ActiveBorder=B(0xFF0A84FF),ActiveInset=B(0x280A84FF),SpecularTop=B(0x12FFFFFF),
  ButtonHover=B(0x14FFFFFF),Pinned=B(0x220A84FF),BadgeFill=B(0xFF2C2C2E),BadgeEdge=B(0xFF1C1C1E),
  Inset=B(0xFF151517),InUse=B(0x240A84FF),OutText=B(0xFF6E6E73),WindowEdge=B(0xFF36363A),ChipActive=B(0x1A0A84FF),
  SwitchOff=B(0xFF39393D),Handle=B(0x59FFFFFF),Track=B(0x24FFFFFF),Drop=B(0x0FFFFFFF);
 // Card min 300 DIP (1 px border each side), gap 14, grid padding 16; sections inset 14 px.
 // Model list sits in a rounded inset (8 px margin), rows 30 px with 3 px margin and 6 px padding:
 // name | 10 | 36 px mini meter | 10 | 34 px percent, 4 rows visible then the list scrolls.
 public const double CardWidth=300,Gap=14,Pad=16,TitleHeight=64,StatusHeight=30,Side=14,ModelRow=30,Mini=32,PctSlot=34,ColGap=10,VisibleModels=4,
  InsetMargin=6,RowMargin=2,RowPad=6,ChipWidth=50,ChipGap=2;
 public const double CardContent=CardWidth-2-2*Side;
 public static double ModelName{get{return CardWidth-2-2*InsetMargin-2*RowMargin-2*RowPad-2*ColGap-Mini-PctSlot;}}
 public static double ModelsMaxHeight{get{return VisibleModels*ModelRow;}}
 public static SolidColorBrush B(uint argb){var b=new SolidColorBrush(Color.FromArgb((byte)(argb>>24),(byte)(argb>>16),(byte)(argb>>8),(byte)argb));b.Freeze();return b;}
 static readonly Dictionary<string,SolidColorBrush> cache=new Dictionary<string,SolidColorBrush>();
 static Color Parse(Provider p){string hex=p==null||string.IsNullOrEmpty(p.Color)?"#F5F5F7":p.Color;try{return (Color)ColorConverter.ConvertFromString(hex);}catch{return Colors.WhiteSmoke;}}
 static SolidColorBrush Cached(string k,Func<Color> make){SolidColorBrush b;if(cache.TryGetValue(k,out b))return b;b=new SolidColorBrush(make());b.Freeze();cache[k]=b;return b;}
 // Provider colour: only the fallback initial logo uses it now; meters and percentages use the Level scale.
 public static SolidColorBrush Fill(Provider p){var c=Parse(p);return Cached("f"+c,()=>c);}
 public static bool Light(Provider p){var c=Parse(p);return 0.299*c.R+0.587*c.G+0.114*c.B>170;}
 // lucide icons (24x24 viewbox), drawn at their own proportions instead of stretched to the box.
 public static readonly Geometry PinIcon=Theme.Icon("M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");
 public static readonly Geometry CloseIcon=Theme.Icon("M18 6 6 18 M6 6l12 12");
 public static readonly Geometry ChevronIcon=Theme.Icon("m6 9 6 6 6-6");
 public static readonly Geometry PlusIcon=Theme.Icon("M12 5v14 M5 12h14");
 // lucide "scan": four corners, the best-fit button.
 public static readonly Geometry FitIcon=Theme.Icon("M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2");
 public static System.Windows.Shapes.Path Icon(Geometry g,double size,Brush stroke){
  var t=g.Clone();t.Transform=new ScaleTransform(size/24,size/24);t.Freeze();
  return new System.Windows.Shapes.Path{Data=t,Stroke=stroke,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=size,Height=size,Stretch=Stretch.None,SnapsToDevicePixels=false,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
 }
}

// Logo of a card's model family; vector paths from T3's Icons.js (OpenAI, Claude, Cursor, Kiro), Gemini's spark.
public static class ModelLogo {
 public enum Kind{Gemini,Claude,OpenAI,OpenCode,Kiro,Cursor,Antigravity,Muse,Other}
 static readonly Geometry GeminiPath=Theme.Icon("M12 0C13.7 6.5 17.5 10.3 24 12C17.5 13.7 13.7 17.5 12 24C10.3 17.5 6.5 13.7 0 12C6.5 10.3 10.3 6.5 12 0Z");
 // OpenCode and Muse Code: the exact vector icons T3 Code ships (provider icon definitions in its client bundle).
 static readonly Geometry OpenCodeFrame=Theme.Icon("M24 8H8V32H24V8ZM32 40H0V0H32V40Z"),OpenCodeCore=Theme.Icon("M24 32H8V16H24V32Z");
 static readonly Geometry MuseLoop=Theme.Icon("m31.06,125.96c0,10.98 2.41,19.41 5.56,24.51 4.13,6.68 10.29,9.51 16.57,9.51 8.1,0 15.51-2.01 29.79-21.76 11.44-15.83 24.92-38.05 33.99-51.98l15.36-23.6c10.67-16.39 23.02-34.61 37.18-46.96 11.56-10.08 24.03-15.68 36.58-15.68 21.07,0 41.14,12.21 56.5,35.11 16.81,25.08 24.97,56.67 24.97,89.27 0,19.38-3.82,33.62-10.32,44.87-6.28,10.88-18.52,21.75-39.11,21.75l0-31.02c17.63,0 22.03-16.2 22.03-34.74 0-26.42-6.16-55.74-19.73-76.69-9.63-14.86-22.11-23.94-35.84-23.94-14.85,0-26.8,11.2-40.23,31.17-7.14,10.61-14.47,23.54-22.7,38.13l-9.06,16.05c-18.2,32.27-22.81,39.62-31.91,51.75-15.95,21.24-29.57,29.29-47.5,29.29-21.27,0-34.72-9.21-43.05-23.09-6.8-11.31-10.14-26.15-10.14-43.06z"),MuseRight=Theme.Icon("m24.49,37.3c14.24-21.95 34.79-37.3 58.36-37.3 13.65,0 27.22,4.04 41.39,15.61 15.5,12.65 32.02,33.48 52.63,67.81l7.39,12.32c17.84,29.72 27.99,45.01 33.93,52.22 7.64,9.26 12.99,12.02 19.94,12.02 17.63,0 22.03-16.2 22.03-34.74l27.4-.86c0,19.38-3.82,33.62-10.32,44.87-6.28,10.88-18.52,21.75-39.11,21.75-12.8,0-24.14-2.78-36.68-14.61-9.64-9.08-20.91-25.21-29.58-39.71l-25.79-43.08c-12.94-21.62-24.81-37.74-31.68-45.04-7.39-7.85-16.89-17.33-32.05-17.33-12.27,0-22.69,8.61-31.41,21.78z"),MuseLeft=Theme.Icon("m82.35,31.23c-12.27,0-22.69,8.61-31.41,21.78-12.33,18.61-19.88,46.33-19.88,72.95 0,10.98 2.41,19.41 5.56,24.51l-26.48,17.44c-6.8-11.31-10.14-26.15-10.14-43.06 0-30.75 8.44-62.8 24.49-87.55 14.24-21.95 34.79-37.3 58.36-37.3z");
 static readonly Brush OpenCodeFg=BoardStyle.B(0xFFF1ECEC),OpenCodeDim=BoardStyle.B(0xFF4B4646),MuseBlue=BoardStyle.B(0xFF0081FB);
 static Brush MuseGradient(double x1,double y1,double x2,double y2,params object[] stops){var g=new LinearGradientBrush{MappingMode=BrushMappingMode.Absolute,StartPoint=new Point(x1,y1),EndPoint=new Point(x2,y2)};for(int i=0;i<stops.Length;i+=2)g.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString((string)stops[i+1]),(double)stops[i]));g.Freeze();return g;}
 static readonly Brush MuseRightFill=MuseGradient(61,117,259,127,0.0,"#0064E1",0.4,"#0064E1",0.83,"#0073EE",1.0,"#0082FB"),MuseLeftFill=MuseGradient(45,139,45,66,0.0,"#0082FB",1.0,"#0064E0");
 static FrameworkElement OpenCodeGlyph(double size){
  var c=new Canvas{Width=32,Height=40};
  c.Children.Add(new System.Windows.Shapes.Path{Data=OpenCodeCore,Fill=OpenCodeDim});c.Children.Add(new System.Windows.Shapes.Path{Data=OpenCodeFrame,Fill=OpenCodeFg});
  double h=Math.Round(size*0.64);return new Viewbox{Stretch=Stretch.Uniform,Width=Math.Round(h*0.8),Height=h,Child=c,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
 }
 static FrameworkElement MuseGlyph(double size){
  var c=new Canvas{Width=287.56,Height=191};
  c.Children.Add(new System.Windows.Shapes.Path{Data=MuseLoop,Fill=MuseBlue});c.Children.Add(new System.Windows.Shapes.Path{Data=MuseRight,Fill=MuseRightFill});c.Children.Add(new System.Windows.Shapes.Path{Data=MuseLeft,Fill=MuseLeftFill});
  double w=Math.Round(size*0.76);return new Viewbox{Stretch=Stretch.Uniform,Width=w,Height=Math.Round(w*191/287.56),Child=c,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
 }
 static readonly Geometry OpenAIPath=Theme.Icon("F0 M252.794 108.802C289.191 99.0484 326.265 110.305 351.148 135.135C385.113 126.072 422.85 134.862 449.492 161.505C476.136 188.149 484.925 225.888 475.862 259.85V259.854C500.696 284.735 511.95 321.81 502.198 358.207C492.447 394.602 464.161 421.084 430.215 430.217C421.083 464.162 394.603 492.448 358.206 502.199C321.812 511.951 284.734 500.693 259.852 475.864C225.887 484.927 188.15 476.137 161.507 449.495C134.864 422.851 126.073 385.111 135.136 351.149C110.304 326.266 99.0496 289.192 108.801 252.795C118.552 216.4 146.84 189.918 180.784 180.785C189.917 146.841 216.396 118.553 252.794 108.802ZM374.292 407.145C374.292 411.271 372.092 415.086 368.517 417.148L283.723 466.102C302.487 480.585 327.555 486.459 352.217 479.852C386.997 470.532 410.068 439.312 410.555 405.006V317.717C410.555 315.08 409.125 312.621 406.843 311.303L374.292 292.509V407.145ZM251.868 415.897C248.296 417.959 243.893 417.959 240.317 415.897L155.526 366.942C152.366 390.436 159.811 415.08 177.866 433.136H177.863C203.325 458.594 241.896 462.962 271.85 446.232L347.449 402.586C349.735 401.268 351.148 398.8 351.148 396.163V358.579L251.868 415.897ZM368.602 220.628C366.319 219.309 363.474 219.318 361.191 220.637L328.641 239.431L427.921 296.749C431.496 298.811 433.697 302.627 433.697 306.752V404.661C455.622 395.654 473.244 376.881 479.851 352.218C489.169 317.442 473.668 281.85 444.201 264.274L368.602 220.628ZM177.303 206.34C155.377 215.348 137.756 234.122 131.148 258.783C121.832 293.561 137.331 329.153 166.799 346.727L242.398 390.373C244.68 391.692 247.525 391.684 249.807 390.366L282.357 371.572L183.078 314.253C179.504 312.189 177.303 308.375 177.303 304.251V206.34ZM259.849 279.145V331.858L305.5 358.213L351.15 331.858V279.145L305.5 252.789L259.849 279.145ZM327.276 144.9C308.512 130.418 283.445 124.543 258.782 131.15C224.002 140.471 200.931 171.691 200.445 205.995V293.286C200.445 295.923 201.875 298.381 204.158 299.7L236.707 318.493V203.856C236.707 199.731 238.909 195.916 242.483 193.853L327.276 144.9ZM433.137 177.867C407.675 152.407 369.103 148.038 339.149 164.769L263.55 208.415C261.265 209.734 259.852 212.202 259.852 214.838V252.423L359.132 195.105C362.703 193.041 367.108 193.041 370.682 195.105L455.473 244.06C458.635 220.567 451.189 195.922 433.135 177.867H433.137Z");
 static readonly Geometry ClaudePath=Theme.Icon("m50.228 170.321 50.357-28.257.843-2.463-.843-1.361h-2.462l-8.426-.518-28.775-.778-24.952-1.037-24.175-1.296-6.092-1.297L0 125.796l.583-3.759 5.12-3.434 7.324.648 16.202 1.101 24.304 1.685 17.629 1.037 26.118 2.722h4.148l.583-1.685-1.426-1.037-1.101-1.037-25.147-17.045-27.22-18.017-14.258-10.37-7.713-5.25-3.888-4.925-1.685-10.758 7-7.713 9.397.649 2.398.648 9.527 7.323 20.35 15.75L94.817 91.9l3.889 3.24 1.555-1.102.195-.777-1.75-2.917-14.453-26.118-15.425-26.572-6.87-11.018-1.814-6.61c-.648-2.723-1.102-4.991-1.102-7.778l7.972-10.823L71.42 0 82.05 1.426l4.472 3.888 6.61 15.101 10.694 23.786 16.591 32.34 4.861 9.592 2.592 8.879.973 2.722h1.685v-1.556l1.36-18.211 2.528-22.36 2.463-28.776.843-8.1 4.018-9.722 7.971-5.25 6.222 2.981 5.12 7.324-.713 4.73-3.046 19.768-5.962 30.98-3.889 20.739h2.268l2.593-2.593 10.499-13.934 17.628-22.036 7.778-8.749 9.073-9.657 5.833-4.601h11.018l8.1 12.055-3.628 12.443-11.342 14.388-9.398 12.184-13.48 18.147-8.426 14.518.778 1.166 2.01-.194 30.46-6.481 16.462-2.982 19.637-3.37 8.88 4.148.971 4.213-3.5 8.62-20.998 5.184-24.628 4.926-36.682 8.685-.454.324.519.648 16.526 1.555 7.065.389h17.304l32.21 2.398 8.426 5.574 5.055 6.805-.843 5.184-12.962 6.611-17.498-4.148-40.83-9.721-14-3.5h-1.944v1.167l11.666 11.406 21.387 19.314 26.767 24.887 1.36 6.157-3.434 4.86-3.63-.518-23.526-17.693-9.073-7.972-20.545-17.304h-1.36v1.814l4.73 6.935 25.017 37.59 1.296 11.536-1.814 3.76-6.481 2.268-7.13-1.297-14.647-20.544-15.1-23.138-12.185-20.739-1.49.843-7.194 77.448-3.37 3.953-7.778 2.981-6.48-4.925-3.436-7.972 3.435-15.749 4.148-20.544 3.37-16.333 3.046-20.285 1.815-6.74-.13-.454-1.49.194-15.295 20.999-23.267 31.433-18.406 19.702-4.407 1.75-7.648-3.954.713-7.064 4.277-6.286 25.47-32.405 15.36-20.092 9.917-11.6-.065-1.686h-.583L44.07 198.125l-12.055 1.555-5.185-4.86.648-7.972 2.463-2.593 20.35-13.999-.064.065Z");
 static readonly Geometry CursorPath=Theme.Icon("M457.43,125.94L244.42,2.96c-6.84-3.95-15.28-3.95-22.12,0L9.3,125.94c-5.75,3.32-9.3,9.46-9.3,16.11v247.99c0,6.65,3.55,12.79,9.3,16.11l213.01,122.98c6.84,3.95,15.28,3.95,22.12,0l213.01-122.98c5.75-3.32,9.3-9.46,9.3-16.11v-247.99c0-6.65-3.55-12.79-9.3-16.11h-.01ZM444.05,151.99l-205.63,356.16c-1.39,2.4-5.06,1.42-5.06-1.36v-233.21c0-4.66-2.49-8.97-6.53-11.31L24.87,145.67c-2.4-1.39-1.42-5.06,1.36-5.06h411.26c5.84,0,9.49,6.33,6.57,11.39h-.01Z");
 static readonly Brush GeminiFill=MakeGemini(),GeminiTile=BoardStyle.B(0xFF1E2A4A),ClaudeTile=BoardStyle.B(0x2ED97757),ClaudeFill=BoardStyle.B(0xFFD97757),
  OpenAITile=BoardStyle.B(0xFFF5F5F7),OpenAIFill=BoardStyle.B(0xFF111113),CursorTile=BoardStyle.B(0xFF2C2C2E),CursorFill=BoardStyle.B(0xFFEDECEC),AgTile=BoardStyle.B(0xFF242428);
 static Brush MakeGemini(){var g=new LinearGradientBrush(Color.FromRgb(0x4F,0x8D,0xF7),Color.FromRgb(0xB0,0x8C,0xF2),new Point(0,0),new Point(1,1));g.Freeze();return g;}
 public static Kind For(string slug,Provider p){
  string s=(slug??"").ToLowerInvariant();
  if(s.Contains("gemini"))return Kind.Gemini;
  if(s.Contains("claude")||s.Contains("opus")||s.Contains("sonnet")||s.Contains("haiku"))return Kind.Claude;
  if(s.StartsWith("gpt")||s.Contains("/gpt")||s.Contains("codex")||Regex.IsMatch(s,@"^o\d"))return Kind.OpenAI;
  switch(p==null?"":p.Driver){case "codex":return Kind.OpenAI;case "claudeAgent":return Kind.Claude;case "opencode":return Kind.OpenCode;case "muse":return Kind.Muse;case "kiro":return Kind.Kiro;case "cursor":return Kind.Cursor;case "antigravity":return Kind.Antigravity;}
  return Kind.Other;
 }
 static FrameworkElement Glyph(Geometry g,double size,Brush fill){return new System.Windows.Shapes.Path{Data=g,Fill=fill,Stretch=Stretch.Uniform,Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,SnapsToDevicePixels=false};}
 static FrameworkElement Picture(string driver,double size){var src=Glyphs.Source(driver);if(src==null)return null;var img=new Image{Source=src,Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};RenderOptions.SetBitmapScalingMode(img,BitmapScalingMode.HighQuality);return img;}
 // size x size tile (radius size/4); Antigravity accounts get their two-letter badge only on top-level tiles (>= 18 px) to keep model rows clean.
 public static FrameworkElement Make(Kind k,Provider p,double size){
  var tile=new Border{Width=size,Height=size,CornerRadius=new CornerRadius(Math.Round(size*0.26,1)),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};
  FrameworkElement inner=null;
  switch(k){
   case Kind.Gemini:tile.Background=GeminiTile;inner=Glyph(GeminiPath,Math.Round(size*0.6),GeminiFill);break;
   case Kind.Claude:tile.Background=ClaudeTile;inner=Glyph(ClaudePath,Math.Round(size*0.62),ClaudeFill);break;
   case Kind.OpenAI:tile.Background=OpenAITile;inner=Glyph(OpenAIPath,Math.Round(size*0.7),OpenAIFill);break;
   case Kind.OpenCode:tile.Background=BoardStyle.B(0xFF242428);inner=OpenCodeGlyph(size);break;
   case Kind.Muse:tile.Background=BoardStyle.B(0xFF242428);inner=MuseGlyph(size);break;
   case Kind.Cursor:tile.Background=CursorTile;inner=Glyph(CursorPath,Math.Round(size*0.58),CursorFill);break;
   case Kind.Kiro:inner=Picture("kiro",size);break;
   case Kind.Antigravity:tile.Background=AgTile;inner=Picture("antigravity",Math.Round(size*0.75));break;
  }
  if(inner==null){
   string l=p==null||string.IsNullOrEmpty(p.Label)?"?":p.Label.Substring(0,1).ToUpperInvariant();
   var t=Theme.Text(l,Math.Round(size*0.5),FontWeights.Bold,BoardStyle.Light(p)?Theme.Popover:Brushes.White);t.TextTrimming=TextTrimming.None;t.HorizontalAlignment=HorizontalAlignment.Center;
   tile.Background=BoardStyle.Fill(p);inner=t;
  }
  tile.Child=inner;
  if(size<18||p==null||p.Driver!="antigravity"||string.IsNullOrEmpty(p.Badge))return tile;
  var badgeText=Theme.Text(p.Badge,7,FontWeights.Bold,Brushes.White);badgeText.TextTrimming=TextTrimming.None;badgeText.LineHeight=10;badgeText.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;
  var badge=new Border{Background=BoardStyle.BadgeFill,BorderBrush=BoardStyle.BadgeEdge,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(2,0,2,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,-3,-3),Child=badgeText};
  var g=new Grid{Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};g.Children.Add(tile);g.Children.Add(badge);return g;
 }
}

// Labels used by the board.
public static class BoardText {
 // "235.57/693" -> "236 / 693".
 public static string Detail(string d){
  if(string.IsNullOrEmpty(d))return "";var parts=d.Split('/');if(parts.Length!=2)return d;
  var r=new string[2];for(int i=0;i<2;i++){double v;if(!double.TryParse(parts[i].Trim().Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out v))return d.Replace("/"," / ");r[i]=Math.Round(v,MidpointRounding.AwayFromZero).ToString("#,0",CultureInfo.InvariantCulture);}
  return r[0]+" / "+r[1];
 }
 // Kiro style, as on Kiro's Account & plan page: "532.9 / 693 used (77%)".
 public static string CreditsUsed(string d){
  if(string.IsNullOrEmpty(d))return "";var parts=d.Split('/');double u,t;
  if(parts.Length!=2||!double.TryParse(parts[0].Trim().Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out u)||!double.TryParse(parts[1].Trim().Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out t)||t<=0)return Detail(d)+" credits";
  var inv=CultureInfo.InvariantCulture;
  return Math.Round(u,1,MidpointRounding.AwayFromZero).ToString("#,0.0",inv)+" / "+Math.Round(t,MidpointRounding.AwayFromZero).ToString("#,0",inv)+" used ("+Math.Round(u*100/t,MidpointRounding.AwayFromZero).ToString(inv)+"%)";
 }
 // Family named in an Antigravity window label ("Claude & GPT (shared) · 5-hour" -> "Claude & GPT"); "" when unlabeled.
 public static string Owner(UsageWindow w){string l=w.Label??"";int i=l.IndexOf('\u00B7');if(i<=0)return "";l=l.Substring(0,i).Trim();int p=l.IndexOf(" (");return p>0?l.Substring(0,p):l;}
 public static bool Credits(UsageWindow w){return w!=null&&(w.Id=="credits"||(w.Label??"").ToLowerInvariant().Contains("credit"));}
 static string Span(string s){switch((s??"").Trim().ToLowerInvariant()){case "5-hour":case "5h":case "five_hour":case "session":return "5h";case "weekly":case "7-day":case "seven_day":case "week":return "Weekly";case "monthly":return "monthly";case "daily":return "daily";case "reported quota":case "quota":return "Quota";}return (s??"").Trim();}
 // "Gemini · 5h", "Claude & GPT · Weekly", "Session · 5h", "Weekly", "Credits · monthly"; unknown labels are kept.
 public static string RowLabel(UsageWindow w){
  string owner=Owner(w);if(owner!="")return owner+" \u00B7 "+Span(UiText.WindowShort(w));
  if(Credits(w))return w.Monthly?"Credits \u00B7 monthly":"Credits";
  string l=(w.Label??w.Id??"").Trim();switch(l.ToLowerInvariant()){
   case "session":case "5-hour":case "5h":case "five_hour":return "Session \u00B7 5h";case "weekly":case "7-day":case "seven_day":return "Weekly";
   case "monthly":return "Monthly";case "daily":return "Daily";case "reported quota":case "quota":return "Quota";}
  return l;
 }
 public static bool FiveHour(UsageWindow w){string l=(UiText.WindowShort(w)??"").ToLowerInvariant();return l=="5-hour"||l=="5h"||l=="five_hour"||l=="session"||(w.Id??"").ToLowerInvariant().Contains("5h")||(w.Id??"")=="five_hour";}
 // T3's order: Gemini, Claude & GPT, Argon; within a family session before daily, weekly, monthly.
 public static int FamilyRank(UsageWindow w){string id=(w.Id??"").ToLowerInvariant();if(id.Contains("argon"))return 2;if(id.StartsWith("gemini"))return 0;if(Regex.IsMatch(id,"^(3p|claude|gpt)"))return 1;return 0;}
 public static int KindRank(UsageWindow w){if(FiveHour(w)||w.Kind=="session")return 0;string k=(w.Kind??"").ToLowerInvariant();string l=(UiText.WindowShort(w)??"").ToLowerInvariant();if(k=="daily"||l=="daily")return 1;if(k=="weekly"||l=="weekly")return 2;if(k=="monthly"||w.Monthly)return 3;return 4;}
 // "96% · reset 2h 41m"; a window that has not started (no reset, 0% used, 5 h) shows its full length.
 public static string Value(UsageWindow w,UsageResult u,DateTime now){
  if(u.AwaitingRefresh)return "awaiting new data";
  string r=Reset(w,now);return u.Left.Value+"%"+(r==""?"":" \u00B7 reset "+r);
 }
 // "2h 41m", "<1m", "5h" for an unstarted 5 h window, "" when the window has no reset time.
 public static string Reset(UsageWindow w,DateTime now){
  if(w==null)return "";
  if(w.ResetsAtUtc.HasValue){var d=w.ResetsAtUtc.Value-now;return d.TotalMinutes<1?"<1m":Format.Duration(d);}
  return w.UsedPercent<=0&&FiveHour(w)?"5h":"";
 }
}

// Focusable element (card or button): button automation peer, optional hover fill and an outside 2 px focus ring.
public class Clickable:Border {
 public string Key="";public Action Invoke;public Brush Normal=Brushes.Transparent,Hover=BoardStyle.ButtonHover;public double RingRadius=8;bool hover;
 static readonly Pen Ring=MakeRing();static Pen MakeRing(){var p=new Pen(Theme.Primary,2);p.Freeze();return p;}
 public Clickable(){
  Focusable=true;FocusVisualStyle=null;Cursor=Cursors.Hand;Background=Brushes.Transparent;
  GotKeyboardFocus+=(s,e)=>InvalidateVisual();LostKeyboardFocus+=(s,e)=>InvalidateVisual();
  MouseEnter+=(s,e)=>{hover=true;Paint();};MouseLeave+=(s,e)=>{hover=false;Paint();};
  MouseLeftButtonUp+=(s,e)=>{if(Invoke==null)return;e.Handled=true;Fire();};
 }
 public void SetNormal(Brush b){Normal=b;Paint();}
 void Paint(){var b=hover&&Hover!=null?Hover:Normal;if(!ReferenceEquals(Background,b))Background=b;}
 public void Fire(){if(Invoke==null)return;try{Invoke();}catch(Exception e){Log.Error("Board action",e);}}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);if(IsKeyboardFocused)dc.DrawRoundedRectangle(null,Ring,new Rect(-2,-2,ActualWidth+4,ActualHeight+4),RingRadius,RingRadius);}
 protected override AutomationPeer OnCreateAutomationPeer(){return new Peer(this);}
 sealed class Peer:FrameworkElementAutomationPeer,IInvokeProvider {
  public Peer(Clickable c):base(c){}
  protected override string GetClassNameCore(){return "Button";}
  protected override AutomationControlType GetAutomationControlTypeCore(){return AutomationControlType.Button;}
  public override object GetPattern(PatternInterface p){return p==PatternInterface.Invoke?this:base.GetPattern(p);}
  public void Invoke(){var c=(Clickable)Owner;c.Dispatcher.BeginInvoke(new Action(c.Fire));}
 }
}

// Usage board. Toolbar: app name, the model in use, a battery strip (one ring per provider that is on, plus "Manage" to turn
// providers on/off) and window buttons. Body: one card per provider that is on, balanced over the rows. Status bar: summary,
// undo and hints. Rings and cards share one order that the user arranges by dragging either one (or Alt+arrows / right-click);
// the order is kept in board-layout.json.
public sealed class Board:Window {
 const string Ns="xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 readonly DataCore data;readonly T3Window tracker;readonly T3Driver driver;
 public event EventHandler HiddenByUser;
 public Snapshot FixedSnapshot;public string FixedPickerName;public bool NoActivateForTest,FixedClosed,KeyboardOpen;
 public bool Pinned{get;private set;}
 public double LastBuildMs;public int Builds;public int Columns{get;private set;}
 // Window size the cards were last fitted to (DIP), and the scale they were drawn at (1 unless the window is too small).
 public double WindowWidth{get{return fitSize.IsEmpty?0:fitSize.Width;}}
 public double FitScale{get{return fitScale;}}
 const int MaxCols=12;
 // Toolbar + status bar + window border, and the grid padding around the cards.
 static double ChromeH{get{return BoardStyle.TitleHeight+BoardStyle.StatusHeight+2;}}
 const double ChromeW=2,MinScale=0.5;
 readonly Border chrome;readonly Grid root;double userW,userH;readonly ScaleTransform scale=new ScaleTransform(1,1);
 readonly Ellipse nowDot;readonly TextBlock nowText;readonly Run nowLead=new Run(),nowModel=new Run(),nowMeta=new Run();readonly Border nowBox;readonly StackPanel center;
 readonly Clickable refreshBtn,pinBtn,closeBtn,launchBtn,addBtn,undoBtn,fitBtn;readonly System.Windows.Shapes.Path pinIcon,refreshIcon;readonly Gauge refreshGauge;readonly TextBlock pinTipText,refreshTipText,statusLeft,statusRight;
 readonly ScrollViewer scroll;readonly Grid grid=new Grid();readonly ScaleTransform gridScale=new ScaleTransform(1,1);
 // Fit data of one card: height without its model list (Base) and how many model rows it has.
 sealed class Fit{public Clickable Card;public ScrollViewer List;public int Rows;public double Base;}
 readonly Dictionary<Clickable,Fit> fits=new Dictionary<Clickable,Fit>();Size fitSize=Size.Empty;double fitScale=1,fitAvailW,fitAvailH;
 const double ListChrome=8;const int MinRows=2,NatCap=8;
 readonly StackPanel strip=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
 readonly DispatcherTimer refreshTimer,noticeTimer,scrollTimer;
 readonly List<Clickable> nav=new List<Clickable>(),cards=new List<Clickable>(),chips=new List<Clickable>();
 readonly Dictionary<Clickable,ScrollViewer> cardRows=new Dictionary<Clickable,ScrollViewer>();readonly Dictionary<string,Border> flashes=new Dictionary<string,Border>();
 string sig="",focusKey,notice;bool noticeError;Action undoAction;
 bool dwm,prewarmed,dragging,resizing,anchored,hasDragPos,manualSize,menuOpen;Int32Rect anchor;int dragX,dragY;Native.POINT openPoint;IntPtr hwnd;
 DateTime refreshStarted=DateTime.MinValue,refreshFullAt=DateTime.MinValue,managerClosedAt=DateTime.MinValue;bool refreshing,refreshDone;int refreshGeneration;Stopwatch openWatch;Rect openT3=Rect.Empty;
 readonly BoardLayout layout;List<string> visibleOrder=new List<string>();
 static ControlTemplate outerTemplate,modelsTemplate;
 readonly Dictionary<string,double> rowOffsets=new Dictionary<string,double>();const int LazyRows=12;
 static readonly IEasingFunction Ease=MakeEase();static IEasingFunction MakeEase(){var e=new CubicEase{EasingMode=EasingMode.EaseOut};e.Freeze();return e;}
 const string Hint="Drag cards or icons to reorder \u00B7 right-click for options";

 // A press on a ring or card; it becomes a drag after 4 px of movement.
 sealed class Press{public Clickable Item;public string Id;public bool Chip,Active,Settling;public Point Start,Grab;public List<string> Original,Work;}
 Press press;
 // "Manage providers" popover.
 Popup manager;StackPanel managerRows;TextBlock managerSummary,managerError;
 readonly Dictionary<string,SwitchView> switches=new Dictionary<string,SwitchView>();readonly Dictionary<string,TextBlock> switchNotes=new Dictionary<string,TextBlock>();readonly Dictionary<string,bool> pending=new Dictionary<string,bool>();
 bool ManagerOpen{get{return manager!=null&&manager.IsOpen;}}

 public Board(DataCore d,T3Window t,T3Driver drv){
  data=d;tracker=t;driver=drv;Columns=MaxCols;layout=BoardLayout.Load();
  Title="Batereai";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;ShowInTaskbar=false;Topmost=true;Width=1200;Height=600;
  Background=Theme.Popover;Foreground=Theme.Foreground;FontFamily=Theme.Font;Theme.Crisp(this);
  // ---- toolbar ----
  var title=new Border{Height=BoardStyle.TitleHeight,Padding=new Thickness(16,0,12,0),BorderBrush=Theme.Border,BorderThickness=new Thickness(0,0,0,1),Background=BoardStyle.SidebarSurface};
  var td=new Grid();td.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});td.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});td.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  var left=new DockPanel{LastChildFill=true,Margin=new Thickness(0,0,16,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(left,0);td.Children.Add(left);
  center=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
  addBtn=AddButton();center.Children.Add(strip);center.Children.Add(addBtn);Grid.SetColumn(center,1);td.Children.Add(center);
  TextBlock ignore;closeBtn=IconButton(BoardStyle.Icon(BoardStyle.CloseIcon,15,Theme.Muted),"Close","Close (Esc)",HideNow,out ignore);
  pinIcon=BoardStyle.Icon(BoardStyle.PinIcon,15,Theme.Muted);pinBtn=IconButton(pinIcon,"Pin","",TogglePin,out pinTipText);
  refreshIcon=BoardStyle.Icon(Theme.RefreshIcon,15,Theme.Muted);refreshGauge=new Gauge(24,2){Visibility=Visibility.Collapsed};
  var refreshVisual=new Grid{Width=24,Height=24,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};refreshVisual.Children.Add(refreshIcon);refreshVisual.Children.Add(refreshGauge);
  refreshBtn=IconButton(refreshVisual,"Refresh","Refresh (R)",DoRefresh,out refreshTipText);
  launchBtn=TextButton("Open T3 Code",Theme.TextXs,new Thickness(10,4,10,4));launchBtn.Key="title:launch";launchBtn.Invoke=OpenNativePicker;launchBtn.Margin=new Thickness(8,0,8,0);launchBtn.VerticalAlignment=VerticalAlignment.Center;launchBtn.Visibility=Visibility.Collapsed;
  var right=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,0,0)};
  fitBtn=IconButton(BoardStyle.Icon(BoardStyle.FitIcon,15,Theme.Muted),"Best fit","Best fit (F) \u00B7 show every card in full",BestFit,out ignore);
  foreach(var b in new[]{launchBtn,fitBtn,refreshBtn,pinBtn,closeBtn})right.Children.Add(b);
  pinBtn.Margin=new Thickness(2,0,2,0);
  Grid.SetColumn(right,2);td.Children.Add(right);
  var appIcon=AppIcon(18);if(appIcon!=null){appIcon.Margin=new Thickness(0,0,8,0);DockPanel.SetDock(appIcon,Dock.Left);left.Children.Add(appIcon);}
  var tt=Theme.Text("Batereai",Theme.TextSm,FontWeights.SemiBold,Theme.Foreground);tt.TextTrimming=TextTrimming.None;DockPanel.SetDock(tt,Dock.Left);left.Children.Add(tt);
  var sep=new Border{Width=1,Height=16,Background=Theme.Border,Margin=new Thickness(14,0,14,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(sep,Dock.Left);left.Children.Add(sep);
  nowBox=new Border{Background=Theme.Accent,CornerRadius=new CornerRadius(8),Padding=new Thickness(10,5,11,5),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
  var now=new DockPanel();
  nowDot=new Ellipse{Width=7,Height=7,Fill=BoardStyle.Ok,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(nowDot,Dock.Left);now.Children.Add(nowDot);
  nowText=Theme.Text("",Theme.Text13,FontWeights.Normal,Theme.Muted);nowModel.Foreground=Theme.Foreground;nowModel.FontWeight=FontWeights.SemiBold;
  nowText.Inlines.Add(nowLead);nowText.Inlines.Add(nowModel);nowText.Inlines.Add(nowMeta);now.Children.Add(nowText);nowBox.Child=now;left.Children.Add(nowBox);
  title.Child=td;title.MouseLeftButtonDown+=TitleDrag;
  // ---- cards ----
  // The grid is sized to the window by FitLayout; it only scrolls when even the minimum scale cannot fit it.
  grid.LayoutTransform=gridScale;grid.HorizontalAlignment=HorizontalAlignment.Left;grid.VerticalAlignment=VerticalAlignment.Top;
  scroll=new ScrollViewer{Content=new Border{Padding=new Thickness(BoardStyle.Pad),Child=grid},VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,CanContentScroll=false,Template=OuterTemplate()};
  // ---- status bar ----
  var status=new Border{Height=BoardStyle.StatusHeight,Padding=new Thickness(16,0,16,0),BorderBrush=Theme.Border,BorderThickness=new Thickness(0,1,0,0),Background=BoardStyle.SidebarSurface};
  var sd=new DockPanel{LastChildFill=true};
  statusRight=Theme.Text(Hint,Theme.Text2xs,FontWeights.Normal,Theme.Muted70);statusRight.Margin=new Thickness(16,0,0,0);DockPanel.SetDock(statusRight,Dock.Right);sd.Children.Add(statusRight);
  statusLeft=Theme.Text("",Theme.Text2xs,FontWeights.Normal,Theme.Muted);statusLeft.TextTrimming=TextTrimming.None;DockPanel.SetDock(statusLeft,Dock.Left);sd.Children.Add(statusLeft);
  var undoText=Theme.Text("Undo",Theme.Text2xs,FontWeights.SemiBold,Theme.Primary);undoText.TextTrimming=TextTrimming.None;
  undoBtn=new Clickable{Child=undoText,Padding=new Thickness(6,2,6,2),CornerRadius=new CornerRadius(5),RingRadius=6,Margin=new Thickness(6,0,0,0),Key="status:undo",Visibility=Visibility.Collapsed,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};
  undoBtn.Invoke=()=>{var u=undoAction;undoAction=null;notice=null;noticeTimer.Stop();if(u!=null)u();sig="";Rebuild();};AutomationProperties.SetName(undoBtn,"Undo");
  sd.Children.Add(undoBtn);status.Child=sd;status.MouseLeftButtonDown+=TitleDrag;
  root=new Grid{RenderTransform=scale};
  root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var body=new Grid();body.Children.Add(scroll);
  Grid.SetRow(body,1);Grid.SetRow(status,2);root.Children.Add(title);root.Children.Add(body);root.Children.Add(status);
  chrome=new Border{Background=Theme.Popover,BorderBrush=BoardStyle.WindowEdge,ClipToBounds=true,Child=root};Content=chrome;
  SetPinVisual();
  SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;Native.ToolWindow(hwnd);dwm=Native.RoundPopup(hwnd,Theme.PopoverBorderOpaque);UseDrawnChrome(!dwm);HwndSource.FromHwnd(hwnd).AddHook(ResizeHook);T3Window.SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x37);};
  // Every size change (edge drag included) refits the cards to the new window.
  SizeChanged+=(s,e)=>{if(IsVisible&&(press==null||!press.Active))FitLayout(ActualWidth,ActualHeight);};
  Deactivated+=(s,e)=>{if(!NoActivateForTest&&!Pinned&&!dragging&&!menuOpen&&!ManagerOpen&&press==null)HideNow();};
  PreviewKeyDown+=OnKey;
  DpiChanged+=(s,e)=>{if(IsVisible&&!dragging)Dispatcher.BeginInvoke(new Action(Place),DispatcherPriority.Send);};
  refreshTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};refreshTimer.Tick+=(s,e)=>RefreshTick();
  noticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(6)};noticeTimer.Tick+=(s,e)=>{noticeTimer.Stop();notice=null;undoAction=null;sig="";Rebuild();};
  scrollTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(16)};scrollTimer.Tick+=AutoScroll;
 }
 IntPtr ResizeHook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){
  // The native resize frame otherwise repaints white when a pinned board loses focus.
  if(msg==0x86){handled=true;return new IntPtr(1);}// WM_NCACTIVATE
  if(msg==0x85){handled=true;return IntPtr.Zero;}// WM_NCPAINT
  if(msg==0x83&&w!=IntPtr.Zero){handled=true;return IntPtr.Zero;}
  if(msg==0x84){ // WM_NCHITTEST: resize from every edge and corner.
   T3Window.RECT r;if(T3Window.GetWindowRect(h,out r)){
    int x=unchecked((short)(l.ToInt64()&0xffff)),y=unchecked((short)((l.ToInt64()>>16)&0xffff));
    int edge=8,side=x<r.L+edge?-1:x>=r.R-edge?1:0,vertical=y<r.T+edge?-1:y>=r.B-edge?1:0;
    int hit=vertical<0?(side<0?13:side>0?14:12):vertical>0?(side<0?16:side>0?17:15):side<0?10:side>0?11:0;
    if(hit!=0){handled=true;return new IntPtr(hit);}
   }
  }
  if(msg==0x231)resizing=true;
  if(msg==0x232){resizing=false;T3Window.RECT r;if(T3Window.GetWindowRect(h,out r)){dragX=r.L;dragY=r.T;hasDragPos=true;}manualSize=true;{double s=VisualTreeHelper.GetDpi(this).DpiScaleX;userW=(r.R-r.L)/s;userH=(r.B-r.T)/s;}Dispatcher.BeginInvoke(new Action(Place),DispatcherPriority.Background);}
  return IntPtr.Zero;
 }
 static ControlTemplate ScrollTemplate(string bar,string thumb){
  return (ControlTemplate)XamlReader.Parse("<ControlTemplate "+Ns+" TargetType='ScrollViewer'><Grid><ScrollContentPresenter/><ScrollBar x:Name='PART_VerticalScrollBar' "+bar+" HorizontalAlignment='Right' Maximum='{TemplateBinding ScrollableHeight}' ViewportSize='{TemplateBinding ViewportHeight}' Value='{TemplateBinding VerticalOffset}' Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'><ScrollBar.Template><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'>"+thumb+"</ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></ScrollBar.Template></ScrollBar></Grid></ControlTemplate>");
 }
 static ControlTemplate OuterTemplate(){if(outerTemplate==null)outerTemplate=ScrollTemplate("Width='6' MinWidth='0' Margin='0,6,4,6'","<Border CornerRadius='3' Background='#38FFFFFF'/>");return outerTemplate;}
 static ControlTemplate ModelsTemplate(){if(modelsTemplate==null)modelsTemplate=ScrollTemplate("Width='4' MinWidth='0' Margin='0,2,2,2'","<Border CornerRadius='2' Background='#38FFFFFF'/>");return modelsTemplate;}

 Clickable IconButton(UIElement icon,string name,string tip,Action act,out TextBlock tipText){
  var b=new Clickable{Width=30,Height=30,CornerRadius=new CornerRadius(8),Child=icon,RingRadius=9,Invoke=act,Key="title:"+name,VerticalAlignment=VerticalAlignment.Center};
  AutomationProperties.SetName(b,name);tipText=Theme.Text(tip,Theme.TextXs,FontWeights.Normal,Theme.Foreground80);tipText.TextTrimming=TextTrimming.None;
  var t=Chrome.Tip(tipText);t.Placement=PlacementMode.Bottom;t.VerticalOffset=4;ToolTipService.SetToolTip(b,t);ToolTipService.SetInitialShowDelay(b,600);return b;
 }
 static Clickable TextButton(string text,double size,Thickness pad){
  var t=Theme.Text(text,size,FontWeights.Medium,Theme.Foreground);t.TextTrimming=TextTrimming.None;
  var b=new Clickable{CornerRadius=new CornerRadius(7),BorderBrush=Theme.Border,BorderThickness=new Thickness(1),Padding=pad,Child=t,Normal=Theme.Accent,Hover=BoardStyle.ButtonHover};b.Background=Theme.Accent;AutomationProperties.SetName(b,text);return b;
 }
 static void Tip(FrameworkElement e,string head,string detail,int delay){
  var sp=new StackPanel();var h=Theme.Text(head,Theme.TextXs,FontWeights.SemiBold,Theme.Foreground);h.TextTrimming=TextTrimming.None;sp.Children.Add(h);
  if(!string.IsNullOrEmpty(detail))foreach(var line in detail.Split('\n')){var d=Theme.Text(line,Theme.TextXs,FontWeights.Normal,Theme.Muted);d.TextTrimming=TextTrimming.None;d.Margin=new Thickness(0,2,0,0);sp.Children.Add(d);}
  var t=Chrome.Tip(sp);t.Placement=PlacementMode.Bottom;t.VerticalOffset=6;ToolTipService.SetToolTip(e,t);ToolTipService.SetInitialShowDelay(e,delay);
 }
 static FrameworkElement AppIcon(double size){
  try{
   string f=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"batereai.ico");if(!System.IO.File.Exists(f))return null;
   var dec=new IconBitmapDecoder(new Uri(f),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
   var frame=dec.Frames.OrderBy(x=>Math.Abs(x.PixelWidth-64)).First();
   var img=new Image{Source=frame,Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};RenderOptions.SetBitmapScalingMode(img,BitmapScalingMode.HighQuality);return img;
  }catch(Exception e){Log.Error("App icon",e);return null;}
 }
 public UIElement Visual{get{return chrome;}}
 public void UseDrawnChrome(bool drawn){chrome.BorderThickness=new Thickness(1);chrome.BorderBrush=BoardStyle.WindowEdge;chrome.CornerRadius=new CornerRadius(12);}
 // ---------- fitting the cards to the window ----------
 // Card height with k model rows visible.
 static double H(Fit f,int k){return f.List==null?f.Base:f.Base+ListChrome+Math.Max(0,k)*BoardStyle.ModelRow;}
 static double MinH(Fit f){return H(f,Math.Min(f.Rows,MinRows));}
 static double NatH(Fit f){return H(f,Math.Min(f.Rows,NatCap));}
 List<Fit> FitOrder(){return cards.Where(fits.ContainsKey).Select(c=>fits[c]).ToList();}
 static List<List<Fit>> RowsOf(List<Fit> list,int c){var rows=new List<List<Fit>>();for(int i=0;i<list.Count;i+=c)rows.Add(list.Skip(i).Take(c).ToList());return rows;}
 // Scores c columns in availW x availH (DIP inside the padding): s = scale needed so every card fits with its minimum list,
 // f = share of the natural list heights that then fits.
 static void Score(List<Fit> list,int c,double availW,double availH,out double s,out double f){
  var rows=RowsOf(list,c);double gaps=(rows.Count-1)*BoardStyle.Gap;
  double needW=c*BoardStyle.CardWidth+(c-1)*BoardStyle.Gap,minH=rows.Sum(r=>r.Max(x=>MinH(x)))+gaps,natH=rows.Sum(r=>r.Max(x=>NatH(x)))+gaps;
  s=Math.Min(1,Math.Min(availW/needW,availH/minH));f=Math.Min(1,(availH/s)/natH);
 }
 int BestColumns(List<Fit> list,double availW,double availH,out double scaleOut){
  int best=1;double bs=-1,bf=-1,bu=-1;
  for(int c=1;c<=Math.Min(MaxCols,list.Count);c++){
   double s,f;Score(list,c,availW,availH,out s,out f);
   // Useful area: card content counts up to 360 DIP wide, so very wide cards (long empty gaps inside) do not win.
   double colW=(availW/Math.Max(s,0.01)-(c-1)*BoardStyle.Gap)/c,u=Math.Round(list.Sum(x=>Math.Min(colW,360)*NatH(x))/1000);
   s=Math.Round(s,3);f=Math.Round(f,2);
   if(s>bs||(s==bs&&(s>=1?c>best:f>bf||(f==bf&&u>=bu)))){best=c;bs=s;bf=f;bu=u;}
  }
  scaleOut=Math.Max(MinScale,bs);return best;
 }
 // Smallest toolbar that still holds the battery strip and the window buttons.
 double MinWindowWidth(){return Math.Max(760,strip.Children.Count*Slot+BoardStyle.ChipWidth+14+2*170);}
 // Best window for maxW x maxH: the smallest window in which every card shows its whole model list (up to NatCap rows);
 // if none fits, the layout that needs the least scaling, at the largest size.
 Size BestWindow(double maxW,double maxH){
  var list=FitOrder();double minW=Math.Min(maxW,MinWindowWidth());
  if(list.Count==0)return new Size(minW,Math.Min(maxH,ChromeH+2*BoardStyle.Pad+220));
  Size best=Size.Empty;double bw=double.MaxValue;
  for(int c=1;c<=Math.Min(MaxCols,list.Count);c++){
   var rows=RowsOf(list,c);double w=Math.Max(minW,ChromeW+2*BoardStyle.Pad+c*BoardStyle.CardWidth+(c-1)*BoardStyle.Gap);
   double h=ChromeH+2*BoardStyle.Pad+rows.Sum(r=>r.Max(x=>NatH(x)))+(rows.Count-1)*BoardStyle.Gap;
   if(w>maxW||h>maxH)continue;
   // The width rule must not let the fit pick a different column count than the one measured here.
   double s;if(BestColumns(list,w-ChromeW-2*BoardStyle.Pad,h-ChromeH-2*BoardStyle.Pad,out s)!=c)continue;
   // Smallest window that shows every card whole.
   double area=w*h;if(area<bw-1){bw=area;best=new Size(Math.Ceiling(w),Math.Ceiling(h));}
  }
  if(!best.IsEmpty)return best;
  // Nothing fits whole: per column count, the window it would need (capped to the screen); keep the one that shows the most.
  double bs=-1,bf=-1;
  for(int c=1;c<=Math.Min(MaxCols,list.Count);c++){
   var rows=RowsOf(list,c);
   double w=Math.Ceiling(Math.Min(maxW,Math.Max(minW,ChromeW+2*BoardStyle.Pad+c*BoardStyle.CardWidth+(c-1)*BoardStyle.Gap)));
   double h=Math.Ceiling(Math.Min(maxH,ChromeH+2*BoardStyle.Pad+rows.Sum(r=>r.Max(x=>NatH(x)))+(rows.Count-1)*BoardStyle.Gap));
   double s,f;Score(list,c,w-ChromeW-2*BoardStyle.Pad,h-ChromeH-2*BoardStyle.Pad,out s,out f);s=Math.Round(s,3);f=Math.Round(f,2);
   double sc;if(BestColumns(list,w-ChromeW-2*BoardStyle.Pad,h-ChromeH-2*BoardStyle.Pad,out sc)!=c)continue;
   if(s>bs||(s==bs&&(f>bf||(f==bf&&w*h<best.Width*best.Height)))){bs=s;bf=f;best=new Size(w,h);}
  }
  if(!best.IsEmpty)return best;
  return new Size(Math.Ceiling(maxW),Math.Ceiling(maxH));
 }
 // Lays the cards out in a winW x winH window: picks the columns, the scale, each row's height and how many model rows
 // each card shows, so everything is visible and lists grow into free space instead of leaving it empty.
 void FitLayout(double winW,double winH){
  if(winW<=0||winH<=0)return;fitSize=new Size(winW,winH);
  double availW=Math.Max(100,winW-ChromeW-2*BoardStyle.Pad),availH=Math.Max(100,winH-ChromeH-2*BoardStyle.Pad);
  // The model in use hides when the toolbar sides get too narrow for it.
  Theme.Show(nowBox,(winW-ChromeW-28-(strip.Children.Count*Slot+BoardStyle.ChipWidth+14))/2>=290);
  var list=FitOrder();
  if(list.Count==0){
   gridScale.ScaleX=gridScale.ScaleY=1;fitScale=1;grid.Width=availW;grid.Height=double.NaN;Columns=1;
   grid.ColumnDefinitions.Clear();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.RowDefinitions.Clear();return;
  }
  double s;int c=BestColumns(list,availW,availH,out s);
  Apply(c,s,availW,availH);
 }
 void Apply(int c,double s,double availW,double availH){
  var list=FitOrder();Columns=c;fitScale=s;fitAvailW=availW;fitAvailH=availH;
  gridScale.ScaleX=gridScale.ScaleY=s;
  var rows=RowsOf(list,c);double gaps=(rows.Count-1)*BoardStyle.Gap;double gw=Math.Floor(availW/s),gh=Math.Floor(availH/s);
  var mins=rows.Select(r=>r.Max(x=>MinH(x))).ToList();var nats=rows.Select(r=>r.Max(x=>NatH(x))).ToList();
  double room=gh-gaps,minSum=mins.Sum();if(minSum>room)room=minSum; // below the minimum scale the grid scrolls instead
  double extra=room-minSum,want=nats.Sum()-minSum;var heights=new List<double>();
  for(int i=0;i<rows.Count;i++){
   double h=want<=extra?nats[i]+(extra-want)/rows.Count:mins[i]+(want<=0?0:(nats[i]-mins[i])*extra/want);
   heights.Add(Math.Floor(h));
  }
  grid.Width=gw;grid.Height=heights.Sum()+gaps;
  if(grid.ColumnDefinitions.Count!=2*c-1){grid.ColumnDefinitions.Clear();for(int i=0;i<c;i++){if(i>0)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(BoardStyle.Gap)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});}}
  grid.RowDefinitions.Clear();
  for(int i=0;i<rows.Count;i++){if(i>0)grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(BoardStyle.Gap)});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(heights[i])});}
  PlaceCards(cards);
  // Each list shows as many whole rows as its card has room for.
  for(int i=0;i<rows.Count;i++)foreach(var f in rows[i]){
   if(f.List==null)continue;int k=(int)Math.Floor((heights[i]-f.Base-ListChrome)/BoardStyle.ModelRow);
   k=Math.Max(Math.Min(f.Rows,1),Math.Min(f.Rows,k));double lh=k*BoardStyle.ModelRow;if(f.List.Height!=lh)f.List.Height=lh;
  }
  if(press==null||!press.Active)Theme.Set(statusRight,s<0.97?"Scaled to "+Math.Round(s*100)+"% so every card fits \u00B7 F for best fit":Hint);
 }
 // Measures each card without its model list (at the narrowest card width, so wider cards never need more).
 void MeasureFits(){
  foreach(var f in fits.Values){
   FrameworkElement inset=f.List==null?null:f.List.Parent as FrameworkElement;
   if(inset!=null)inset.Visibility=Visibility.Collapsed;
   f.Card.Measure(new Size(BoardStyle.CardWidth,double.PositiveInfinity));f.Base=Math.Ceiling(f.Card.DesiredSize.Height);
   if(inset!=null)inset.Visibility=Visibility.Visible;
  }
 }
 // --selftest: lay out for a window of the given size, or the best window within maxW x maxH.
 public Size FitForTest(double maxW,double maxH){var b=BestWindow(maxW,maxH);FitLayout(b.Width,b.Height);return b;}
 public void LayoutForTest(double w,double h){FitLayout(w,h);}
 void BestFit(){manualSize=false;Log.Info("Board best fit");if(IsVisible)Place();}
 Snapshot Snap{get{return data!=null?data.Current:FixedSnapshot;}}
 string PickerName{get{return tracker!=null?tracker.Current.PickerName:FixedPickerName;}}
 bool T3Closed{get{return FixedClosed||(tracker!=null&&!tracker.Current.Found);}}

 public void Prewarm(){if(prewarmed)return;prewarmed=true;new WindowInteropHelper(this).EnsureHandle();Rebuild();var b=BestWindow(1900,1000);FitLayout(b.Width,b.Height);chrome.Measure(b);chrome.Arrange(new Rect(b));}
 public void SnapshotChanged(){if(IsVisible)Rebuild();}
 public void MinuteTick(){if(IsVisible)Rebuild();}
 // An unpinned board opened at the pill closes when T3 moves, resizes or hides (the pill moves away); otherwise refresh state.
 public void TrackerChanged(){
  if(!IsVisible)return;
  if(tracker!=null&&!Pinned&&anchored&&!hasDragPos&&!openT3.IsEmpty&&press==null){var s=tracker.Current;if(!s.Visible||s.WindowRect!=openT3){HideNow();return;}}
  Rebuild();
 }

 // ---------- open / close / placement ----------
 public void ShowAt(bool toPill,Int32Rect pill,Stopwatch sw){
  openWatch=sw;anchored=toPill&&!pill.IsEmpty;anchor=pill;hasDragPos=false;Native.GetCursorPos(out openPoint);openT3=tracker!=null&&tracker.Current.Visible?tracker.Current.WindowRect:Rect.Empty;
  new WindowInteropHelper(this).EnsureHandle();Rebuild();Place();
  root.RenderTransformOrigin=new Point(0.5,0.5);
  var fade=Spline(0,1);var grow=Spline(0.98,1);root.BeginAnimation(OpacityProperty,fade);scale.BeginAnimation(ScaleTransform.ScaleXProperty,grow);scale.BeginAnimation(ScaleTransform.ScaleYProperty,grow);
  ShowActivated=!NoActivateForTest;Show();
  if(!NoActivateForTest){Activate();Native.SetForegroundWindow(hwnd);Focus();if(KeyboardOpen)FocusInitial();}
  CompositionTarget.Rendering+=FirstFrame;
 }
 void FirstFrame(object s,EventArgs e){CompositionTarget.Rendering-=FirstFrame;var sw=openWatch;openWatch=null;if(sw!=null)Dispatcher.BeginInvoke(new Action(()=>Log.Info("Board open "+sw.Elapsed.TotalMilliseconds.ToString("0.0",CultureInfo.InvariantCulture)+" ms")),DispatcherPriority.Render);}
 static DoubleAnimationUsingKeyFrames Spline(double from,double to){
  var a=new DoubleAnimationUsingKeyFrames{Duration=Theme.Open,FillBehavior=FillBehavior.Stop};
  a.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));a.KeyFrames.Add(new SplineDoubleKeyFrame(to,KeyTime.FromTimeSpan(Theme.Open.TimeSpan),new KeySpline(0.25,0.1,0.25,1)));a.Freeze();return a;
 }
 public void HideNow(){
  if(!IsVisible)return;
  if(press!=null&&press.Active&&!press.Settling)EndDrag(false);
  if(ManagerOpen)manager.IsOpen=false;
  Hide();
  var h=HiddenByUser;if(h!=null)h(this,EventArgs.Empty);
 }
 // Closes an unpinned board once focus has really left the app (menus and the provider popover keep it open).
 void CheckFocusLost(){
  if(!IsVisible||Pinned||NoActivateForTest||menuOpen||ManagerOpen||IsActive||press!=null)return;
  uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return;
  HideNow();
 }
 // Physical pixels: centred on the work area of the monitor showing the pill (else T3's window, else the cursor).
 // A drag while open wins until the board closes. Height fits content up to 85% of the work area.
 void Place(){
  if(hwnd==IntPtr.Zero||resizing||(press!=null&&press.Active))return;uint dpi;Int32Rect wa;
  if(hasDragPos)wa=Native.WorkAreaAt(dragX+20,dragY+20,out dpi);
  else if(anchored)wa=Native.WorkAreaAt(anchor.X+anchor.Width/2,anchor.Y+anchor.Height/2,out dpi);
  else if(!openT3.IsEmpty&&openT3.Width>0)wa=Native.WorkAreaAt((int)(openT3.X+openT3.Width/2),(int)(openT3.Y+openT3.Height/2),out dpi);
  else wa=Native.WorkAreaAt(openPoint.X,openPoint.Y,out dpi);
  if(dpi==0)dpi=96;double s=dpi/96.0;int margin=(int)Math.Round(8*s);
  double maxH=Math.Floor(wa.Height*0.9/s),maxW=Math.Floor((wa.Width-2*margin)/s),hardH=Math.Floor((wa.Height-2*margin)/s);
  MinWidth=Math.Min(maxW,MinWindowWidth());MinHeight=Math.Min(hardH,300);MaxWidth=maxW;MaxHeight=hardH;
  // Until the user resizes it, the window takes the best-fit size; afterwards the cards follow the user's size.
  double wd,hd;
  if(manualSize){wd=Math.Round(Math.Max(MinWidth,Math.Min(MaxWidth,userW)));hd=Math.Round(Math.Max(MinHeight,Math.Min(MaxHeight,userH)));}
  else{var b=BestWindow(maxW,maxH);wd=Math.Round(Math.Max(MinWidth,b.Width));hd=Math.Round(Math.Max(MinHeight,b.Height));}
  root.Width=double.NaN;root.Height=double.NaN;
  if(Width!=wd)Width=wd;if(Height!=hd)Height=hd;FitLayout(wd,hd);
  int w=(int)Math.Round(wd*s),h=(int)Math.Round(hd*s),x,y;
  if(hasDragPos){x=dragX;y=dragY;}
  else{x=wa.X+(wa.Width-w)/2;y=wa.Y+(wa.Height-h)/2;}
  x=Math.Max(wa.X+margin,Math.Min(wa.X+wa.Width-margin-w,x));y=Math.Max(wa.Y+margin,Math.Min(wa.Y+wa.Height-margin-h,y));
  T3Window.SetWindowPos(hwnd,new IntPtr(-1),x,y,w,h,Native.NoActivate);
  if(ManagerOpen)Dispatcher.BeginInvoke(new Action(NudgeManager),DispatcherPriority.Loaded);
 }
 void TitleDrag(object s,MouseButtonEventArgs e){
  for(var d=e.OriginalSource as DependencyObject;d!=null;d=d is Visual?VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d))if(d is Clickable)return;
  if(e.ButtonState!=MouseButtonState.Pressed||hwnd==IntPtr.Zero)return;
  if(e.ClickCount==2){e.Handled=true;BestFit();return;} // double-click the toolbar = best fit
  dragging=true;try{DragMove();}catch(InvalidOperationException){}finally{dragging=false;}
  T3Window.RECT r;if(T3Window.GetWindowRect(hwnd,out r)){dragX=r.L;dragY=r.T;hasDragPos=true;Place();}
 }

 // ---------- keyboard ----------
 void OnKey(object s,KeyEventArgs e){
  var key=e.Key==Key.System?e.SystemKey:e.Key;var mods=Keyboard.Modifiers;
  if(press!=null&&press.Active){if(key==Key.Escape){EndDrag(false);e.Handled=true;}return;}
  // Alt+arrows / Alt+Home/End move the focused card or ring.
  if((mods&ModifierKeys.Alt)!=0){
   var f=Focused;string id=f==null?null:ProviderOf(f.Key);bool card=f!=null&&f.Key.StartsWith("card:");int delta=0;
   switch(key){case Key.Left:delta=-1;break;case Key.Right:delta=1;break;case Key.Up:delta=card?-Columns:-1;break;case Key.Down:delta=card?Columns:1;break;case Key.Home:delta=int.MinValue;break;case Key.End:delta=int.MaxValue;break;}
   if(id!=null&&delta!=0){MoveBy(id,delta);e.Handled=true;}
   return;
  }
  bool other=(mods&(ModifierKeys.Control|ModifierKeys.Windows))!=0;
  switch(e.Key){
   case Key.Escape:if(ManagerOpen)manager.IsOpen=false;else HideNow();break;
   case Key.R:if(other)return;DoRefresh();break;
   case Key.P:if(other)return;TogglePin();break;
   case Key.M:if(other)return;ToggleManager();break;
   case Key.F:if(other)return;BestFit();break;
   case Key.Tab:Move((mods&ModifierKeys.Shift)!=0?-1:1);break;
   case Key.Right:Move(1);break;case Key.Left:Move(-1);break;
   case Key.Down:Vertical(1);break;case Key.Up:Vertical(-1);break;
   case Key.Home:if(cards.Count>0)FocusOn(cards[0]);break;case Key.End:if(cards.Count>0)FocusOn(cards[cards.Count-1]);break;
   case Key.PageDown:Page(1);break;case Key.PageUp:Page(-1);break;
   case Key.Enter:case Key.Space:var fc=Focused;if(fc!=null)fc.Fire();else FocusInitial();break;
   default:return;
  }
  e.Handled=true;
 }
 static string ProviderOf(string key){return key!=null&&(key.StartsWith("card:")||key.StartsWith("chip:"))?key.Substring(5):null;}
 Clickable Focused{get{var f=Keyboard.FocusedElement as Clickable;return f!=null&&nav.Contains(f)?f:null;}}
 void FocusOn(Clickable c){if(c==null)return;c.Focus();Keyboard.Focus(c);c.BringIntoView();}
 public void FocusInitial(){FocusOn(cards.FirstOrDefault()??nav.FirstOrDefault());}
 void Move(int dir){var cur=Focused;if(cur==null){FocusInitial();return;}if(nav.Count==0)return;int i=nav.IndexOf(cur);FocusOn(nav[(i+dir+nav.Count)%nav.Count]);}
 // Up/Down: the card in the same column of the previous/next row.
 void Vertical(int dir){var cur=Focused;int i=cur==null?-1:cards.IndexOf(cur);if(i<0){FocusInitial();return;}int j=i+dir*Columns;if(j>=0&&j<cards.Count)FocusOn(cards[j]);else if(dir>0&&j>=cards.Count&&i/Columns<(cards.Count-1)/Columns)FocusOn(cards[cards.Count-1]);}
 // PageUp/PageDown: the focused card's own rows when they scroll, else the grid.
 void Page(int dir){var cur=Focused;ScrollViewer sv;if(cur!=null&&cardRows.TryGetValue(cur,out sv)&&sv.ScrollableHeight>0){if(dir>0)sv.PageDown();else sv.PageUp();return;}if(dir>0)scroll.PageDown();else scroll.PageUp();}

 // ---------- arranging ----------
 // delta: +-n positions, int.MinValue = first, int.MaxValue = last.
 void MoveBy(string id,int delta){
  var list=new List<string>(visibleOrder);int i=list.IndexOf(id);if(i<0)return;
  int j=delta==int.MinValue?0:delta==int.MaxValue?list.Count-1:Math.Max(0,Math.Min(list.Count-1,i+delta));if(i==j)return;
  list.RemoveAt(i);list.Insert(j,id);layout.SetVisibleOrder(list);layout.Save();sig="";Rebuild();
 }
 static ScaleTransform Lift(UIElement e){return (ScaleTransform)((TransformGroup)e.RenderTransform).Children[0];}
 static TranslateTransform Shift(UIElement e){return (TranslateTransform)((TransformGroup)e.RenderTransform).Children[1];}
 static void To(IAnimatable a,DependencyProperty p,double v,int ms){a.BeginAnimation(p,new DoubleAnimation(v,new Duration(TimeSpan.FromMilliseconds(ms))){EasingFunction=Ease});}
 static void Slide(TranslateTransform t,double fx,double fy){
  var d=new Duration(TimeSpan.FromMilliseconds(240));
  t.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(fx,0,d){EasingFunction=Ease});t.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(fy,0,d){EasingFunction=Ease});
 }
 void Draggable(Clickable e){
  var g=new TransformGroup();g.Children.Add(new ScaleTransform(1,1));g.Children.Add(new TranslateTransform());e.RenderTransform=g;e.RenderTransformOrigin=new Point(0.5,0.5);
  e.MouseMove+=PressMove;e.PreviewMouseLeftButtonUp+=PressUp;e.LostMouseCapture+=PressLost;
 }
 void BeginPress(Clickable item,string id,bool chip,MouseButtonEventArgs e){
  if(press!=null)return;
  press=new Press{Item=item,Id=id,Chip=chip,Start=e.GetPosition(chip?(UIElement)strip:grid)};
 }
 void PressMove(object s,MouseEventArgs e){
  var p=press;if(p==null||p.Settling||!ReferenceEquals(s,p.Item))return;
  if(e.LeftButton!=MouseButtonState.Pressed){if(!p.Active)press=null;return;}
  var pos=e.GetPosition(p.Chip?(UIElement)strip:grid);
  if(!p.Active){if(Math.Abs(pos.X-p.Start.X)<4&&Math.Abs(pos.Y-p.Start.Y)<4)return;StartDrag(p);if(press==null)return;}
  DragTo(pos);
 }
 void PressUp(object s,MouseButtonEventArgs e){
  var p=press;if(p==null||!ReferenceEquals(s,p.Item))return;
  if(p.Active){e.Handled=true;EndDrag(true);}else if(!p.Settling)press=null;
 }
 void PressLost(object s,MouseEventArgs e){var p=press;if(p!=null&&p.Active&&!p.Settling&&ReferenceEquals(s,p.Item))EndDrag(true);}
 void StartDrag(Press p){
  p.Original=new List<string>(visibleOrder);p.Work=new List<string>(visibleOrder);
  if(!p.Work.Contains(p.Id)){press=null;return;}
  p.Active=true;var off=VisualTreeHelper.GetOffset(p.Item);p.Grab=new Point(p.Start.X-off.X,p.Start.Y-off.Y);
  Panel.SetZIndex(p.Item,100);p.Item.CaptureMouse();Mouse.OverrideCursor=Cursors.SizeAll;
  double k=p.Chip?1.12:1.025;To(Lift(p.Item),ScaleTransform.ScaleXProperty,k,140);To(Lift(p.Item),ScaleTransform.ScaleYProperty,k,140);
  if(!p.Chip){p.Item.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=30,ShadowDepth=12,Direction=270,Opacity=0.55,Color=Colors.Black};scrollTimer.Start();}
  Theme.Set(statusRight,"Release to drop \u00B7 Esc to cancel");
 }
 void DragTo(Point pos){
  var p=press;if(p==null||!p.Active||p.Settling)return;
  int target=p.Chip?ChipTarget(pos):CardTarget(pos);int cur=p.Work.IndexOf(p.Id);
  if(target!=cur){p.Work.RemoveAt(cur);p.Work.Insert(target,p.Id);if(p.Chip)LayoutChips(p);else LayoutCards(p);}
  var off=VisualTreeHelper.GetOffset(p.Item);var t=Shift(p.Item);
  t.BeginAnimation(TranslateTransform.XProperty,null);t.BeginAnimation(TranslateTransform.YProperty,null);
  t.X=pos.X-p.Grab.X-off.X;t.Y=p.Chip?0:pos.Y-p.Grab.Y-off.Y;
 }
 static double Slot{get{return BoardStyle.ChipWidth+BoardStyle.ChipGap;}}
 int ChipTarget(Point pos){return Math.Max(0,Math.Min(chips.Count-1,(int)Math.Floor(pos.X/Slot)));}
 int CardTarget(Point pos){
  int rows=(grid.RowDefinitions.Count+1)/2;if(rows==0)return 0;int row=rows-1;
  for(int r=0;r<rows;r++){var rd=grid.RowDefinitions[r*2];if(pos.Y<rd.Offset+rd.ActualHeight+BoardStyle.Gap/2){row=r;break;}}
  int col=Columns-1;for(int c=0;c<Columns;c++){var cd=grid.ColumnDefinitions[c*2];if(pos.X<cd.Offset+cd.ActualWidth+BoardStyle.Gap/2){col=c;break;}}
  return Math.Max(0,Math.Min(cards.Count-1,row*Columns+col));
 }
 // Rings keep their children order while dragging (re-parenting would drop the mouse capture); each slides to its new slot.
 void LayoutChips(Press p){
  for(int i=0;i<chips.Count;i++){var c=chips[i];if(c==p.Item)continue;int at=p.Work.IndexOf(ProviderOf(c.Key));To(Shift(c),TranslateTransform.XProperty,(at-i)*Slot,220);}
 }
 // Cards move to their new grid cells, then slide there from where they were (FLIP).
 void LayoutCards(Press p){
  var before=cards.ToDictionary(c=>c,c=>VisualTreeHelper.GetOffset(c));var byId=cards.ToDictionary(c=>ProviderOf(c.Key));
  var order=p.Work.Where(byId.ContainsKey).Select(id=>byId[id]).ToList();cards.Clear();cards.AddRange(order);
  Apply(Columns,fitScale,fitAvailW,fitAvailH);grid.UpdateLayout(); // same columns; row heights follow the new rows
  foreach(var c in order){
   if(c==p.Item)continue;var a=before[c];var b=VisualTreeHelper.GetOffset(c);double dx=a.X-b.X,dy=a.Y-b.Y;
   if(Math.Abs(dx)<0.5&&Math.Abs(dy)<0.5)continue;var t=Shift(c);Slide(t,t.X+dx,t.Y+dy);
  }
  cards.Clear();cards.AddRange(order);
 }
 void PlaceCards(IList<Clickable> list){
  for(int i=0;i<list.Count;i++){int row=i/Columns,col=i%Columns;Grid.SetRow(list[i],row*2);Grid.SetColumn(list[i],col*2);list[i].Margin=new Thickness(0);}
 }
 void EndDrag(bool commit){
  var p=press;if(p==null||!p.Active||p.Settling)return;p.Settling=true;scrollTimer.Stop();Mouse.OverrideCursor=null;
  if(!commit){p.Work=new List<string>(p.Original);if(p.Chip)LayoutChips(p);else LayoutCards(p);}
  if(p.Item.IsMouseCaptured)p.Item.ReleaseMouseCapture();
  var t=Shift(p.Item);double tx=p.Chip?(p.Work.IndexOf(p.Id)-chips.IndexOf(p.Item))*Slot:0;var d=new Duration(TimeSpan.FromMilliseconds(220));
  var ax=new DoubleAnimation(t.X,tx,d){EasingFunction=Ease};ax.Completed+=(s,e)=>Finish(p,commit);
  t.BeginAnimation(TranslateTransform.XProperty,ax);t.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(t.Y,0,d){EasingFunction=Ease});
  To(Lift(p.Item),ScaleTransform.ScaleXProperty,1,220);To(Lift(p.Item),ScaleTransform.ScaleYProperty,1,220);
  var fx=p.Item.Effect as System.Windows.Media.Effects.DropShadowEffect;if(fx!=null)fx.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,new DoubleAnimation(0,d));
 }
 void Finish(Press p,bool commit){
  if(press!=p)return;press=null;
  if(commit&&!p.Work.SequenceEqual(p.Original)){layout.SetVisibleOrder(p.Work);layout.Save();Log.Info("Board order "+string.Join(",",p.Work.ToArray()));}
  p.Item.Effect=null;Panel.SetZIndex(p.Item,0);Theme.Set(statusRight,Hint);
  sig="";Rebuild();
 }
 // Dragging a card near the top or bottom edge scrolls the grid.
 void AutoScroll(object s,EventArgs e){
  var p=press;if(p==null||!p.Active||p.Chip||p.Settling){scrollTimer.Stop();return;}
  double y=Mouse.GetPosition(scroll).Y,edge=48,speed=0;
  if(y<edge)speed=-Math.Min(1,(edge-y)/edge)*16;else if(y>scroll.ActualHeight-edge)speed=Math.Min(1,(y-(scroll.ActualHeight-edge))/edge)*16;
  if(speed==0||scroll.ScrollableHeight<=0)return;
  double was=scroll.VerticalOffset;scroll.ScrollToVerticalOffset(Math.Max(0,Math.Min(scroll.ScrollableHeight,was+speed)));scroll.UpdateLayout();
  if(scroll.VerticalOffset!=was)DragTo(Mouse.GetPosition(grid));
 }
 static bool Interactive(DependencyObject d,DependencyObject stop){
  for(;d!=null&&d!=stop;d=d is Visual?VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d))if(d is Clickable||d is ScrollBar||d is Thumb)return true;
  return false;
 }
 // Card or ring context menu: move, collapse the model list, manage providers, turn this provider off.
 void ShowMenu(Provider p,FrameworkElement target,bool isCard){
  int i=visibleOrder.IndexOf(p.Id),n=visibleOrder.Count;var m=Chrome.Menu();string id=p.Id;var prov=p;
  MenuAdd(m,"Move to start",i>0,()=>MoveBy(id,int.MinValue));
  MenuAdd(m,"Move left",i>0,()=>MoveBy(id,-1));
  MenuAdd(m,"Move right",i>=0&&i<n-1,()=>MoveBy(id,1));
  MenuAdd(m,"Move to end",i>=0&&i<n-1,()=>MoveBy(id,int.MaxValue));
  m.Items.Add(Chrome.Line());
  if(isCard)MenuAdd(m,layout.Collapsed.Contains(id)?"Show model list":"Collapse model list",true,()=>ToggleModels(id));
  MenuAdd(m,"Manage providers\u2026",true,OpenManager);
  m.Items.Add(Chrome.Line());
  MenuAdd(m,"Turn off "+p.Label,data!=null,()=>SetEnabled(prov,false,true));
  m.PlacementTarget=target;m.Placement=PlacementMode.MousePoint;Theme.Crisp(m);
  m.Opened+=(s,e)=>menuOpen=true;m.Closed+=(s,e)=>{menuOpen=false;Dispatcher.BeginInvoke(new Action(CheckFocusLost),DispatcherPriority.Background);};
  m.IsOpen=true;
 }
 static void MenuAdd(ContextMenu m,string text,bool enabled,Action a){var i=Chrome.Item(text,a);if(!enabled){i.IsEnabled=false;i.Opacity=0.4;}m.Items.Add(i);}
 void ToggleModels(string id){if(!layout.Collapsed.Add(id))layout.Collapsed.Remove(id);layout.Save();sig="";Rebuild();}
 void SetEnabled(Provider p,bool on,bool undo){
  if(data==null)return;var prov=p;
  try{data.SetProviderEnabled(p.Id,on);Notice(p.Label+(on?" turned on":" turned off"),false,undo?(Action)(()=>SetEnabled(prov,!on,false)):null);}
  catch(Exception e){Log.Error("Provider toggle",e);Notice("Could not change "+p.Label+": "+e.Message,true,null);}
 }
 void Notice(string text,bool error,Action undo){notice=text;noticeError=error;undoAction=undo;noticeTimer.Stop();noticeTimer.Start();sig="";Rebuild();}
 void RevealCard(string id){
  var card=cards.FirstOrDefault(c=>c.Key=="card:"+id);if(card==null)return;card.BringIntoView();Border f;if(!flashes.TryGetValue(id,out f))return;
  var a=new DoubleAnimationUsingKeyFrames();
  a.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)),new KeySpline(0.25,0.1,0.25,1)));
  a.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
  a.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1200)),new KeySpline(0.25,0.1,0.25,1)));
  f.BeginAnimation(OpacityProperty,a);
 }

 // ---------- provider manager (popover under "Manage") ----------
 void ToggleManager(){if(ManagerOpen){manager.IsOpen=false;return;}if((DateTime.UtcNow-managerClosedAt).TotalMilliseconds<300)return;OpenManager();}
 void OpenManager(){
  var snap=Snap;if(snap==null)return;if(manager==null)CreateManager();
  pending.Clear();Theme.Show(managerError,false);BuildManagerRows(snap);manager.IsOpen=true;
 }
 void CreateManager(){
  manager=new Popup{AllowsTransparency=true,StaysOpen=false,Placement=PlacementMode.Custom,PlacementTarget=addBtn,PopupAnimation=PopupAnimation.Fade};
  manager.CustomPopupPlacementCallback=(popup,target,offset)=>new[]{new CustomPopupPlacement(new Point(Math.Round((target.Width-popup.Width)/2),target.Height-8),PopupPrimaryAxis.Horizontal)};
  manager.Closed+=(s,e)=>{managerClosedAt=DateTime.UtcNow;pending.Clear();Dispatcher.BeginInvoke(new Action(CheckFocusLost),DispatcherPriority.Background);};
  var panel=new StackPanel();
  var head=new Grid{Margin=new Thickness(18,16,18,4)};head.ColumnDefinitions.Add(new ColumnDefinition());head.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var ht=Theme.Text("Provider",Theme.TextMd,FontWeights.SemiBold,Theme.Foreground);head.Children.Add(ht);
  managerSummary=Theme.Text("",Theme.Text2xs,FontWeights.Medium,Theme.Muted);managerSummary.TextTrimming=TextTrimming.None;Grid.SetColumn(managerSummary,1);head.Children.Add(managerSummary);panel.Children.Add(head);
  var desc=Theme.Text("Providers that are on appear in Batereai and in the T3 Code model picker.",Theme.Text2xs,FontWeights.Normal,Theme.Muted70);desc.TextWrapping=TextWrapping.Wrap;desc.TextTrimming=TextTrimming.None;desc.Margin=new Thickness(18,0,18,8);panel.Children.Add(desc);
  managerRows=new StackPanel();
  var sv=new ScrollViewer{Content=managerRows,MaxHeight=460,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Focusable=false,Template=ModelsTemplate()};panel.Children.Add(sv);
  managerError=Theme.Text("",Theme.Text2xs,FontWeights.Normal,Theme.ErrorForeground);managerError.TextWrapping=TextWrapping.Wrap;managerError.TextTrimming=TextTrimming.None;managerError.Margin=new Thickness(18,6,18,0);managerError.Visibility=Visibility.Collapsed;panel.Children.Add(managerError);
  var box=new Border{Width=340,Background=BoardStyle.Card,BorderBrush=BoardStyle.B(0x26FFFFFF),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(0,0,0,8),Margin=new Thickness(16),Child=panel,
   Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=32,ShadowDepth=10,Direction=270,Opacity=0.5,Color=Colors.Black}};
  box.SetValue(TextElement.FontFamilyProperty,Theme.Font);Theme.Crisp(box);manager.Child=box;
 }
 // --selftest: the popover content, detached so it can be rendered to a PNG.
 public FrameworkElement ManagerVisual(){var snap=Snap;if(snap==null)return null;if(manager==null)CreateManager();BuildManagerRows(snap);var b=(FrameworkElement)manager.Child;manager.Child=null;return b;}
 void NudgeManager(){if(!ManagerOpen)return;manager.HorizontalOffset+=0.01;manager.HorizontalOffset-=0.01;}
 // Rows stay where they were while the popover is open, so a row never jumps away from the pointer after a click.
 void BuildManagerRows(Snapshot snap){
  managerRows.Children.Clear();switches.Clear();switchNotes.Clear();var now=DateTime.UtcNow;
  var on=layout.Arrange(snap.Providers.Where(p=>p.Enabled));
  var off=snap.Providers.Where(p=>!p.Enabled).OrderBy(p=>p.Label,StringComparer.CurrentCultureIgnoreCase).ToList();
  if(on.Count>0){managerRows.Children.Add(Section("On"));foreach(var p in on)managerRows.Children.Add(ManagerRow(p,snap,now));}
  if(off.Count>0){managerRows.Children.Add(Section("Off"));foreach(var p in off)managerRows.Children.Add(ManagerRow(p,snap,now));}
  if(snap.Providers.Count==0){var t=Theme.Text("No providers in ~/.t3/caches yet.",Theme.Text13,FontWeights.Normal,Theme.Muted);t.Margin=new Thickness(18,8,18,8);managerRows.Children.Add(t);}
  ManagerSummary(snap);
 }
 static TextBlock Section(string text){var t=Theme.Text(text,Theme.Text2xs,FontWeights.SemiBold,Theme.Muted);t.Margin=new Thickness(18,10,18,4);return t;}
 FrameworkElement ManagerRow(Provider p,Snapshot snap,DateTime now){
  var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  g.Children.Add(ModelLogo.Make(ModelLogo.For(null,p),p,28));
  var names=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(names,2);
  names.Children.Add(Theme.Text(p.Label,Theme.Text13,FontWeights.Medium,Theme.Foreground));
  var note=Theme.Text("",Theme.Text2xs,FontWeights.Normal,Theme.Muted);note.Margin=new Thickness(0,1,0,0);names.Children.Add(note);g.Children.Add(names);
  var sw=new SwitchView(p.Enabled);Grid.SetColumn(sw,4);g.Children.Add(sw);
  var row=new Clickable{Height=48,Padding=new Thickness(12,0,12,0),Margin=new Thickness(6,0,6,0),CornerRadius=new CornerRadius(9),RingRadius=10,Child=g,Key="manage:"+p.Id};
  string id=p.Id,label=p.Label;row.Invoke=()=>Flip(id,label);
  switches[id]=sw;switchNotes[id]=note;ManagerNote(p,snap,now,p.Enabled);
  AutomationProperties.SetName(row,(p.Enabled?"Turn off ":"Turn on ")+p.Label);
  return row;
 }
 void ManagerNote(Provider p,Snapshot snap,DateTime now,bool on){
  TextBlock t;if(!switchNotes.TryGetValue(p.Id,out t))return;
  if(!on){Theme.Set(t,"Off");return;}
  var head=UsageMath.CardHeadline(p,UsageMath.CardModel(snap,p,snap.Active),snap.Active,now);
  Theme.Set(t,Format.Left(head)+(head!=null&&!head.AwaitingRefresh&&BoardText.Reset(head.Window,now)!=""?" \u00B7 \u21BB "+BoardText.Reset(head.Window,now):""));
 }
 void ManagerSummary(Snapshot snap){
  int on=snap.Providers.Count(p=>{bool v;return pending.TryGetValue(p.Id,out v)?v:p.Enabled;});
  Theme.Set(managerSummary,on+" of "+snap.Providers.Count+" on");
 }
 void Flip(string id,string label){
  SwitchView sw;if(!switches.TryGetValue(id,out sw))return;bool next=!sw.On;sw.Set(next,true);pending[id]=next;Theme.Show(managerError,false);
  var snap=Snap;var p=snap==null?null:snap.Providers.FirstOrDefault(x=>x.Id==id);if(p!=null)ManagerNote(p,snap,DateTime.UtcNow,next);if(snap!=null)ManagerSummary(snap);
  if(data==null)return;
  try{data.SetProviderEnabled(id,next);}
  catch(Exception e){Log.Error("Provider toggle",e);pending.Remove(id);sw.Set(!next,true);if(p!=null)ManagerNote(p,snap,DateTime.UtcNow,!next);managerError.Text="Could not change "+label+": "+e.Message;Theme.Show(managerError,true);}
 }
 // New data while the popover is open: switches follow T3's settings unless a click is still being written.
 void RefreshManager(Snapshot snap){
  if(snap.Providers.Any(p=>!switches.ContainsKey(p.Id))||switches.Keys.Any(k=>!snap.Providers.Any(p=>p.Id==k))){BuildManagerRows(snap);return;}
  var now=DateTime.UtcNow;
  foreach(var p in snap.Providers){
   bool want=p.Enabled,pend;
   if(pending.TryGetValue(p.Id,out pend)){if(pend==want)pending.Remove(p.Id);else continue;}
   switches[p.Id].Set(want,true);ManagerNote(p,snap,now,want);
  }
  ManagerSummary(snap);
 }

 // ---------- content ----------
 Clickable AddButton(){
  var ring=new Grid{Width=36,Height=36,HorizontalAlignment=HorizontalAlignment.Center};
  ring.Children.Add(new Ellipse{Stroke=BoardStyle.Handle,StrokeThickness=1.5,StrokeDashArray=new DoubleCollection{2.5,2.5},StrokeDashCap=PenLineCap.Round,Margin=new Thickness(1.5)});
  ring.Children.Add(BoardStyle.Icon(BoardStyle.PlusIcon,16,Theme.Muted));
  var cap=Theme.Text("Manage",Theme.Text2xs,FontWeights.Medium,Theme.Muted);cap.TextTrimming=TextTrimming.None;cap.HorizontalAlignment=HorizontalAlignment.Center;cap.Margin=new Thickness(0,3,0,0);
  var col=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};col.Children.Add(ring);col.Children.Add(cap);
  var b=new Clickable{Width=BoardStyle.ChipWidth+6,Height=56,CornerRadius=new CornerRadius(10),RingRadius=11,Child=col,Key="title:manage",Padding=new Thickness(0,4,0,0),Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};
  b.Invoke=ToggleManager;AutomationProperties.SetName(b,"Manage providers");Tip(b,"Manage providers (M)","Turn providers on or off",400);
  return b;
 }
 // Ring in the toolbar: remaining quota of the provider's card model around its logo, percentage underneath.
 Clickable Chip(Provider p,Snapshot snap,Active a,bool active,DateTime now){
  var head=UsageMath.CardHeadline(p,UsageMath.CardModel(snap,p,a),a,now);int? left=head==null?null:head.Left;bool stale=UiText.IsStale(p,now);
  var gauge=new Gauge(36,3);gauge.Set(left.HasValue?left.Value/100.0:-1,stale?(Brush)Level.Stale:Level.Fill(left));
  var logo=ModelLogo.Make(ModelLogo.For(null,p),p,18);logo.HorizontalAlignment=HorizontalAlignment.Center;logo.VerticalAlignment=VerticalAlignment.Center;
  var ring=new Grid{Width=36,Height=36,HorizontalAlignment=HorizontalAlignment.Center};ring.Children.Add(gauge);ring.Children.Add(logo);
  var pct=NumText(Theme.Text2xs);pct.HorizontalAlignment=HorizontalAlignment.Center;pct.Margin=new Thickness(0,3,0,0);
  if(left.HasValue)pct.Inlines.Add(R(left.Value+"%",stale?Theme.Muted:Level.Fill(left),FontWeights.SemiBold));else pct.Inlines.Add(R("\u2014",Theme.Muted,FontWeights.SemiBold));
  var col=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};col.Children.Add(ring);col.Children.Add(pct);
  var chip=new Clickable{Width=BoardStyle.ChipWidth,Height=56,CornerRadius=new CornerRadius(10),RingRadius=11,Child=col,Key="chip:"+p.Id,Padding=new Thickness(0,4,0,0),Margin=new Thickness(BoardStyle.ChipGap/2,0,BoardStyle.ChipGap/2,0),VerticalAlignment=VerticalAlignment.Center};
  if(active)chip.SetNormal(BoardStyle.ChipActive);
  Draggable(chip);string id=p.Id;var prov=p;
  chip.Invoke=()=>{if(press!=null&&press.Active)return;RevealCard(id);};
  chip.PreviewMouseLeftButtonDown+=(s,e)=>BeginPress(chip,id,true,e);
  chip.MouseRightButtonUp+=(s,e)=>{e.Handled=true;ShowMenu(prov,chip,false);};
  string reset=head==null||head.AwaitingRefresh?"":BoardText.Reset(head.Window,now);
  string detail=Format.Left(head)+(reset!=""?" \u00B7 \u21BB "+reset:"")+(active?"\nIn use":"")+(stale?"\nStale":"")+"\nDrag to reorder";
  Tip(chip,p.Label,detail,450);
  AutomationProperties.SetName(chip,p.Label+", "+Format.Left(head)+(active?", in use":"")+". Alt+arrow keys to move.");
  return chip;
 }
 // Rebuilds only when the snapshot, minute, T3 state, notice or refresh indicator changes; otherwise a no-op.
 public void Rebuild(){
  var snap=Snap;if(snap==null)return;
  if(press!=null){if(press.Active||press.Settling)return;press=null;}
  var now=DateTime.UtcNow;bool closed=T3Closed;string picker=PickerName??"";
  string s=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(snap)+"|"+now.Ticks/TimeSpan.TicksPerMinute+"|"+closed+"|"+picker+"|"+refreshing+"|"+notice;
  if(s==sig)return;sig=s;var sw=Stopwatch.StartNew();
  var fc=Focused;if(fc!=null)focusKey=fc.Key;else if(!IsKeyboardFocusWithin)focusKey=null;
  var a=closed?null:snap.Active;Provider act=a==null?null:snap.Providers.FirstOrDefault(p=>p.Id==a.ProviderId);
  BuildTitle(a,act,closed,picker);refreshIcon.Stroke=refreshing?Theme.Primary:Theme.Muted;
  var ordered=layout.Arrange(snap.Providers.Where(p=>p.Enabled));visibleOrder=ordered.Select(p=>p.Id).ToList();
  // Model lists keep their scroll position across the minute/snapshot rebuilds.
  rowOffsets.Clear();foreach(var kv in cardRows)rowOffsets[kv.Key.Key]=kv.Value.VerticalOffset;
  nav.Clear();cards.Clear();chips.Clear();cardRows.Clear();flashes.Clear();fits.Clear();strip.Children.Clear();
  grid.Children.Clear();grid.RowDefinitions.Clear();grid.ColumnDefinitions.Clear();
  foreach(var p in ordered){var chip=Chip(p,snap,a,p==act,now);strip.Children.Add(chip);chips.Add(chip);nav.Add(chip);}
  nav.Add(addBtn);
  for(int i=0;i<ordered.Count;i++){
   Clickable toggle;Fit fit;var card=Card(ordered[i],snap,a,ordered[i]==act,now,out toggle,out fit);grid.Children.Add(card);cards.Add(card);fits[card]=fit;nav.Add(card);if(toggle!=null)nav.Add(toggle);
  }
  if(ordered.Count==0)grid.Children.Add(EmptyState());
  MeasureFits();
  if(IsVisible)FitLayout(ActualWidth,ActualHeight);else if(!fitSize.IsEmpty)FitLayout(fitSize.Width,fitSize.Height);
  if(launchBtn.Visibility==Visibility.Visible)nav.Add(launchBtn);nav.Add(fitBtn);nav.Add(refreshBtn);nav.Add(pinBtn);nav.Add(closeBtn);
  BuildStatus(snap,ordered,now);
  if(ManagerOpen)RefreshManager(snap);
  if(focusKey!=null){var again=nav.FirstOrDefault(c=>c.Key==focusKey);if(again!=null&&IsVisible)FocusOn(again);}
  LastBuildMs=sw.Elapsed.TotalMilliseconds;Builds++;
  if(IsVisible)Place();
 }
 FrameworkElement EmptyState(){
  var sp=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,36,0,40)};
  var t1=Theme.Text("All providers are off",Theme.TextMd,FontWeights.SemiBold,Theme.Foreground);t1.HorizontalAlignment=HorizontalAlignment.Center;sp.Children.Add(t1);
  var t2=Theme.Text("Turn on a provider to see its remaining quota here.",Theme.Text13,FontWeights.Normal,Theme.Muted);t2.HorizontalAlignment=HorizontalAlignment.Center;t2.Margin=new Thickness(0,6,0,14);sp.Children.Add(t2);
  var b=TextButton("Manage providers\u2026",Theme.Text13,new Thickness(14,6,14,6));b.Key="empty:manage";b.HorizontalAlignment=HorizontalAlignment.Center;b.Invoke=OpenManager;sp.Children.Add(b);nav.Add(b);
  return sp;
 }
 void BuildStatus(Snapshot snap,List<Provider> ordered,DateTime now){
  if(notice!=null){Theme.Set(statusLeft,notice);Theme.Set(statusLeft,noticeError?Theme.ErrorForeground:Theme.Foreground80);Theme.Show(undoBtn,undoAction!=null);if(undoAction!=null)nav.Add(undoBtn);return;}
  Theme.Show(undoBtn,false);Theme.Set(statusLeft,Theme.Muted);
  DateTime? newest=ordered.Where(p=>p.CheckedAtUtc.HasValue).Select(p=>p.CheckedAtUtc).DefaultIfEmpty(null).Max();
  Theme.Set(statusLeft,ordered.Count+" of "+snap.Providers.Count+" providers on"+(newest.HasValue?" \u00B7 updated "+UiText.Age(newest,now):""));
 }

 void BuildTitle(Active a,Provider act,bool closed,string picker){
  Theme.Show(launchBtn,closed);
  if(closed){nowDot.Fill=BoardStyle.Dim;Set(nowLead,"T3 Code is not open");Set(nowModel,"");Set(nowMeta,"");}
  else if(a==null){nowDot.Fill=BoardStyle.Dim;Set(nowLead,"No active thread yet");Set(nowModel,"");Set(nowMeta,"");}
  else{
   nowDot.Fill=BoardStyle.Ok;string opts=UiText.Options(a,picker);string label=act==null?a.ProviderId:act.Label;
   Set(nowLead,"");Set(nowModel,a.ModelName??a.ModelSlug??"");Set(nowMeta,(opts==""?"":" \u00B7 "+opts)+" \u00B7 "+label);
  }
  string full=nowLead.Text+nowModel.Text+nowMeta.Text;AutomationProperties.SetName(nowText,"In use: "+full);
  if(nowText.ToolTip==null){var tip=Chrome.Tip(Theme.Text("",Theme.TextXs,FontWeights.Normal,Theme.Foreground80));tip.Placement=PlacementMode.Bottom;nowText.ToolTip=tip;}
  var tt=(TextBlock)((ToolTip)nowText.ToolTip).Content;tt.TextTrimming=TextTrimming.None;Theme.Set(tt,a==null?full:"In use: "+full);
 }
 static void Set(Run r,string t){if(r.Text!=(t??""))r.Text=t??"";}

 // Card: header (logo, name, model line, big remaining percentage), quota windows, collapsible model list, footer.
 // A grab handle shows on hover; drag anywhere outside the buttons and lists to move the card.
 Clickable Card(Provider p,Snapshot snap,Active a,bool active,DateTime now,out Clickable modelToggle,out Fit fit){
  string slug=UsageMath.CardModel(snap,p,a);var m=slug==null?null:p.Models.FirstOrDefault(x=>x.Slug==slug);
  string modelName=active&&a!=null?(a.ModelName??slug):m!=null?m.Name:slug;
  var head=UsageMath.CardHeadline(p,slug,a,now);bool stale=UiText.IsStale(p,now);
  var card=new Clickable{Key="card:"+p.Id,CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=active?BoardStyle.ActiveBorder:BoardStyle.CardEdge,Hover=null,RingRadius=15,Cursor=null,VerticalAlignment=VerticalAlignment.Stretch};
  card.SetNormal(active?BoardStyle.CardElevated:BoardStyle.Card);Draggable(card);fit=new Fit{Card=card};
  var inner=new Border{CornerRadius=new CornerRadius(13),BorderThickness=new Thickness(0,1,0,0),BorderBrush=active?BoardStyle.ActiveInset:BoardStyle.SpecularTop};
  var handle=new Border{Width=30,Height=4,CornerRadius=new CornerRadius(2),Background=BoardStyle.Handle,Opacity=0,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,6,0,0),IsHitTestVisible=false};
  var flash=new Border{CornerRadius=new CornerRadius(13),BorderThickness=new Thickness(2),BorderBrush=BoardStyle.ActiveBorder,Opacity=0,IsHitTestVisible=false};
  var layer=new Grid();layer.Children.Add(inner);layer.Children.Add(handle);layer.Children.Add(flash);card.Child=layer;flashes[p.Id]=flash;
  card.MouseEnter+=(s,e)=>To(handle,OpacityProperty,1,150);card.MouseLeave+=(s,e)=>{if(press==null||press.Item!=card)To(handle,OpacityProperty,0,200);};
  string id=p.Id;var prov=p;
  card.PreviewMouseLeftButtonDown+=(s,e)=>{if(Interactive(e.OriginalSource as DependencyObject,card))return;BeginPress(card,id,false,e);};
  card.MouseRightButtonUp+=(s,e)=>{e.Handled=true;ShowMenu(prov,card,true);};
  var dock=new DockPanel{LastChildFill=false};inner.Child=dock;
  var credit=p.Windows.FirstOrDefault(w=>BoardText.Credits(w)&&!string.IsNullOrEmpty(w.Detail));
  var parts=new List<string>();if(stale&&p.CheckedAtUtc.HasValue)parts.Add("stale");if(credit!=null)parts.Add(BoardText.CreditsUsed(credit.Detail));
  parts.Add(p.CheckedAtUtc.HasValue?"updated "+UiText.Age(p.CheckedAtUtc,now):"never updated");
  string updateInfo=string.Join(" \u00B7 ",parts.ToArray());
  // 1. Header.
  var hg=new Grid{Margin=new Thickness(BoardStyle.Side,14,BoardStyle.Side,12)};
  hg.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});hg.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});hg.ColumnDefinitions.Add(new ColumnDefinition());hg.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});hg.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  hg.Children.Add(ModelLogo.Make(ModelLogo.For(null,p),p,28));
  var names=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(names,2);hg.Children.Add(names);
  var label=Theme.Text(p.Label,Theme.TextSm,FontWeights.SemiBold,Theme.Foreground);names.Children.Add(label);
  var sub=new TextBlock{FontFamily=Theme.Font,FontSize=Theme.Text2xs,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)};
  string subText;
  if(active){sub.Inlines.Add(R("\u25CF ",BoardStyle.Ok,FontWeights.Normal));sub.Inlines.Add(R("In use",BoardStyle.Ok,FontWeights.SemiBold));if(!string.IsNullOrEmpty(modelName))sub.Inlines.Add(R(" \u00B7 "+modelName,Theme.Muted,FontWeights.Normal));subText="In use \u00B7 "+modelName;}
  else if(!string.IsNullOrEmpty(modelName)){sub.Inlines.Add(R("Last: "+modelName,Theme.Muted,FontWeights.Normal));subText="Last used: "+modelName;}
  else{sub.Inlines.Add(R("Not used yet",Theme.Muted70,FontWeights.Normal));subText="Not used yet";}
  if(stale)subText+=" \u00B7 stale (older than 30 minutes)";
  names.Children.Add(sub);Tip(names,p.Label,subText,700);
  var big=NumText(22);big.HorizontalAlignment=HorizontalAlignment.Right;big.TextAlignment=TextAlignment.Right;
  string reset=head==null||head.AwaitingRefresh?"":BoardText.Reset(head.Window,now);
  if(head!=null&&!head.AwaitingRefresh){var c=stale?(Brush)Theme.Muted:Level.Fill(head.Left);big.Inlines.Add(R(head.Left.Value.ToString(CultureInfo.InvariantCulture),c,FontWeights.SemiBold));big.Inlines.Add(new Run("%"){Foreground=c,FontWeight=FontWeights.Medium,FontSize=14});}
  else big.Inlines.Add(R("\u2014",Theme.Muted,FontWeights.SemiBold));
  var small=Theme.Text(head==null?"no quota":head.AwaitingRefresh?"awaiting data":reset!=""?"\u21BB "+reset:"left",Theme.Text2xs,FontWeights.Normal,Theme.Muted);small.TextTrimming=TextTrimming.None;small.HorizontalAlignment=HorizontalAlignment.Right;small.Margin=new Thickness(0,1,SwitchView.W+14,0);
  var enabledSwitch=new SwitchView(p.Enabled);var enabledButton=new Clickable{Width=SwitchView.W+8,Height=28,Padding=new Thickness(4,4,4,4),Margin=new Thickness(6,0,0,0),CornerRadius=new CornerRadius(7),RingRadius=8,Child=enabledSwitch,Normal=Brushes.Transparent,Hover=BoardStyle.ButtonHover,VerticalAlignment=VerticalAlignment.Center,Key="provider:"+p.Id};
  enabledButton.Invoke=()=>SetEnabled(prov,!prov.Enabled,true);AutomationProperties.SetName(enabledButton,(p.Enabled?"Turn off ":"Turn on ")+p.Label);Tip(enabledButton,p.Enabled?"Provider on":"Provider off",p.Enabled?"Click to turn the provider off":"Click to turn the provider on",400);
  var percentageRow=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};percentageRow.Children.Add(big);percentageRow.Children.Add(enabledButton);
  var rightCol=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};rightCol.Children.Add(percentageRow);rightCol.Children.Add(small);Grid.SetColumn(rightCol,4);hg.Children.Add(rightCol);
  Tip(rightCol,head==null?p.Label:Format.Left(head),(head==null?"":(head.AwaitingRefresh?"":Format.ResetLong(head.Window,now)+"\n")+"Window: "+BoardText.RowLabel(head.Window)+"\n")+updateInfo,500);
  DockPanel.SetDock(hg,Dock.Top);dock.Children.Add(hg);
  // 2. Quota windows (family, then session, daily, weekly, monthly).
  var wins=new StackPanel{Margin=new Thickness(BoardStyle.Side,0,BoardStyle.Side,0)};
  if(p.Windows.Count==0)wins.Children.Add(Theme.Text("quota not reported",Theme.TextXs,FontWeights.Normal,Theme.Muted));
  else{
   var list=p.Windows.Select((w,i)=>new{w,i}).OrderBy(x=>BoardText.FamilyRank(x.w)).ThenBy(x=>BoardText.KindRank(x.w)).ThenBy(x=>x.i).Select(x=>x.w).ToList();
   for(int i=0;i<list.Count;i++){var r=Row(list[i],now,stale);if(i>0)r.Margin=new Thickness(0,2,0,0);wins.Children.Add(r);}
  }
  DockPanel.SetDock(wins,Dock.Top);dock.Children.Add(wins);
  // 3. Model list behind a disclosure header; collapsed state is remembered.
  bool isCollapsed=layout.Collapsed.Contains(p.Id);
  var mt=new Grid();mt.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});mt.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});mt.ColumnDefinitions.Add(new ColumnDefinition());mt.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  mt.Children.Add(SectionLabel("Model"));
  var mc=Theme.Text(p.Models.Count.ToString(CultureInfo.InvariantCulture),Theme.Text2xs,FontWeights.Medium,Theme.Muted70);mc.TextTrimming=TextTrimming.None;mc.Margin=new Thickness(6,0,0,0);Grid.SetColumn(mc,1);mt.Children.Add(mc);
  var chev=BoardStyle.Icon(BoardStyle.ChevronIcon,12,Theme.Muted);chev.RenderTransformOrigin=new Point(0.5,0.5);chev.RenderTransform=new RotateTransform(isCollapsed?-90:0);Grid.SetColumn(chev,3);mt.Children.Add(chev);
  var toggle=new Clickable{Key="models:"+p.Id,Margin=new Thickness(BoardStyle.Side-6,1,BoardStyle.Side-6,0),Padding=new Thickness(6,2,6,2),CornerRadius=new CornerRadius(6),RingRadius=7,Child=mt,Normal=Brushes.Transparent,Hover=BoardStyle.ButtonHover};
  toggle.Invoke=()=>ToggleModels(id);
  AutomationProperties.SetName(toggle,(isCollapsed?"Show":"Collapse")+" model list "+p.Label);
  DockPanel.SetDock(toggle,Dock.Top);dock.Children.Add(toggle);modelToggle=toggle;
  if(!isCollapsed){
   string inUse=active&&a!=null&&!string.IsNullOrEmpty(a.ModelSlug)?a.ModelSlug:null;
   var models=p.Models.ToList();if(inUse!=null&&!models.Any(x=>x.Slug==inUse))models.Add(new UsageModel(inUse,a.ModelName??inUse,"{}"));
   models=models.OrderBy(x=>x.Name??x.Slug,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.Slug,StringComparer.OrdinalIgnoreCase).ToList();
   var mrows=new StackPanel();
   if(models.Count==0){var t=Theme.Text("no models shown",Theme.TextXs,FontWeights.Normal,Theme.Muted);t.Height=BoardStyle.ModelRow;t.Padding=new Thickness(BoardStyle.RowMargin+BoardStyle.RowPad,0,0,0);mrows.Children.Add(t);}
   // Rows are built lazily: the first LazyRows now, the rest as they scroll into view; a spacer keeps the extent exact.
   int built=Math.Min(models.Count,LazyRows);for(int i=0;i<built;i++)mrows.Children.Add(ModelRow(p,models[i],models[i].Slug==inUse,now,stale));
   Border spacer=null;if(built<models.Count){spacer=new Border{Height=(models.Count-built)*BoardStyle.ModelRow};mrows.Children.Add(spacer);}
   var inset=new Border{Background=BoardStyle.Inset,CornerRadius=new CornerRadius(10),Margin=new Thickness(BoardStyle.InsetMargin,0,BoardStyle.InsetMargin,1),Padding=new Thickness(0,1,0,1)};
   // The list's height (whole rows) is set by FitLayout from the room left in the card; it scrolls when shorter than its rows.
   {
    var sv=new ScrollViewer{Content=mrows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Focusable=false,Height=Math.Min(Math.Max(1,models.Count),BoardStyle.VisibleModels)*BoardStyle.ModelRow,Template=ModelsTemplate()};
    fit.List=sv;fit.Rows=Math.Max(1,models.Count);
    if(spacer!=null)sv.ScrollChanged+=(s,e)=>{
     int need=Math.Min(models.Count,(int)Math.Ceiling((sv.VerticalOffset+Math.Max(sv.ViewportHeight,BoardStyle.ModelsMaxHeight))/BoardStyle.ModelRow)+LazyRows);
     if(need<=built)return;
     for(;built<need;built++)mrows.Children.Insert(mrows.Children.Count-1,ModelRow(p,models[built],models[built].Slug==inUse,now,stale));
     spacer.Height=(models.Count-built)*BoardStyle.ModelRow;if(built>=models.Count)mrows.Children.Remove(spacer);
    };
    double keep;if(rowOffsets.TryGetValue(card.Key,out keep)&&keep>0)sv.ScrollToVerticalOffset(keep);
    sv.PreviewMouseWheel+=ForwardWheel;inset.Child=sv;cardRows[card]=sv;
   }
   DockPanel.SetDock(inset,Dock.Top);dock.Children.Add(inset);
  }
  AutomationProperties.SetName(card,p.Label+", "+(head==null?"quota not reported":head.AwaitingRefresh?"awaiting new data":head.Left.Value+"% left")+", model "+(string.IsNullOrEmpty(modelName)?"not used yet":modelName)+(active?", in use":"")+(stale?", stale":"")+". Alt+arrow keys to move.");
  return card;
 }
 static TextBlock SectionLabel(string text){var t=Theme.Text(text,Theme.Text2xs,FontWeights.SemiBold,Theme.Muted);t.TextTrimming=TextTrimming.None;return t;}
 static TextBlock NumText(double size){var t=new TextBlock{FontFamily=Theme.Font,FontSize=size,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.None};Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);return t;}
 static Run R(string text,Brush fg,FontWeight w){return new Run(text){Foreground=fg,FontWeight=w};}
 // Model row (30): logo, name (and a "in use" tag) | 36 px mini meter | percent of THAT model.
 // 0% left: dimmed name and logo, red 0%, empty meter. Click switches T3 to this model.
 FrameworkElement ModelRow(Provider p,UsageModel m,bool inUse,DateTime now,bool stale){
  var u=UsageMath.ModelUsage(p,m.Slug,now);int? left=u==null?null:u.Left;bool spent=left.HasValue&&left.Value<=0;
  var g=new Grid();
  g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(BoardStyle.ColGap)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(BoardStyle.Mini)});
  g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(BoardStyle.ColGap)});g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(BoardStyle.PctSlot)});
  var namePanel=new DockPanel{LastChildFill=true,VerticalAlignment=VerticalAlignment.Center};
  var logo=ModelLogo.Make(ModelLogo.For(m.Slug,p),p,15);if(spent)logo.Opacity=0.4;DockPanel.SetDock(logo,Dock.Left);namePanel.Children.Add(logo);
  if(inUse){
   var tt=Theme.Text("in use",10,FontWeights.SemiBold,BoardStyle.Ok);tt.TextTrimming=TextTrimming.None;
   var tag=new Border{Background=BoardStyle.B(0x2430D158),CornerRadius=new CornerRadius(4),Padding=new Thickness(5,1,5,1),Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,Child=tt};
   DockPanel.SetDock(tag,Dock.Right);namePanel.Children.Add(tag);
  }
  var weight=inUse?FontWeights.SemiBold:FontWeights.Normal;string name=string.IsNullOrEmpty(m.Name)?m.Slug:m.Name;
  var n=Theme.Text(name,Theme.Text13,weight,spent?BoardStyle.OutText:inUse?Theme.Foreground:Theme.Foreground80);n.Margin=new Thickness(7,0,0,0);namePanel.Children.Add(n);
  if(Theme.Measure(name,Theme.Text13,weight)>BoardStyle.ModelName-22-(inUse?56:0))Tip(n,name,null,500);
  g.Children.Add(namePanel);
  var mini=new Meter(BoardStyle.Mini,4);mini.Set(spent?0:UiText.Fraction(u),Level.Meter(left,stale));Grid.SetColumn(mini,2);g.Children.Add(mini);
  var pct=Theme.Text(left.HasValue?left.Value+"%":"\u2014",Theme.TextXs,FontWeights.SemiBold,stale&&left.HasValue?Theme.Muted:Level.Fill(left));pct.TextTrimming=TextTrimming.None;pct.TextAlignment=TextAlignment.Right;pct.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(pct,4);g.Children.Add(pct);
  var row=new Clickable{Height=BoardStyle.ModelRow,Margin=new Thickness(BoardStyle.RowMargin,0,BoardStyle.RowMargin,0),Padding=new Thickness(BoardStyle.RowPad,0,BoardStyle.RowPad,0),CornerRadius=new CornerRadius(7),RingRadius=8,Child=g,Key="model:"+p.Id+":"+m.Slug,Cursor=Cursors.Hand,Hover=BoardStyle.ButtonHover};
  row.SetNormal(inUse?BoardStyle.InUse:Brushes.Transparent);row.Invoke=()=>SelectModel(p,m);
  AutomationProperties.SetName(row,"Select "+name+" in "+p.Label+(inUse?", in use":""));
  return row;
 }
 void SelectModel(Provider p,UsageModel m){
  if(driver==null||tracker==null||!tracker.Current.Found)return;
  var h=tracker.Current.Hwnd;if(h!=IntPtr.Zero)Native.SetForegroundWindow(h);
  driver.RunAsync("select",p.Label,m.Name??m.Slug,null,null).ContinueWith(t=>Dispatcher.BeginInvoke(new Action(()=>{
   string result=t.IsFaulted?"error:"+t.Exception.GetBaseException().Message:t.Result;
   if(result=="ok"){data.SetSelectedModel(p.Id,m.Slug);return;}
   Log.Info("Select model: "+result);Notice("Could not select "+(m.Name??m.Slug)+": "+(result.StartsWith("error:")?result.Substring(6):result),true,null);
  })));
 }
 // Window row: label left, "NN%" in the level colour + muted " · ↻ 3h 22m" right, 4 px capsule meter underneath.
 static FrameworkElement Row(UsageWindow w,DateTime now,bool stale){
  var u=new UsageResult(w,now);bool depleted=u.Left.HasValue&&u.Left.Value==0;var row=new Grid{Height=20,Background=Brushes.Transparent,ClipToBounds=true};
  var m=new Meter(double.NaN,18){HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,1,0,1),IsHitTestVisible=false};m.Set(UiText.Fraction(u),Level.QuotaMeter(u.Left,stale));row.Children.Add(m);
  var line=new Grid{Margin=new Thickness(8,0,8,0),VerticalAlignment=VerticalAlignment.Center};line.ColumnDefinitions.Add(new ColumnDefinition());line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var label=Theme.Text(BoardText.RowLabel(w),Theme.TextXs,FontWeights.Medium,depleted?Theme.ErrorForeground:Theme.Foreground);label.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetColumn(label,0);line.Children.Add(label);
  var v=NumText(Theme.TextXs);v.Margin=new Thickness(8,0,0,0);Grid.SetColumn(v,1);
  if(u.AwaitingRefresh)v.Inlines.Add(R("awaiting new data",Theme.Foreground80,FontWeights.Normal));
  else{v.Inlines.Add(R(u.Left.Value+"%",depleted?Theme.ErrorForeground:Theme.Foreground,FontWeights.SemiBold));string r=BoardText.Reset(w,now);if(r!="")v.Inlines.Add(R("  \u21BB "+r,Theme.Foreground80,FontWeights.Normal));}
  line.Children.Add(v);row.Children.Add(line);
  string longReset=Format.ResetLong(w,now);if(longReset!="")Tip(row,BoardText.RowLabel(w)+" \u00B7 "+Format.Left(u),longReset,600);
  return row;
 }
 // A card's rows keep the wheel while they can scroll; at either end the grid scrolls instead.
 void ForwardWheel(object s,MouseWheelEventArgs e){
  var sv=(ScrollViewer)s;bool up=e.Delta>0;bool can=up?sv.VerticalOffset>0.5:sv.VerticalOffset<sv.ScrollableHeight-0.5;if(can)return;
  e.Handled=true;if(scroll.ScrollableHeight>0)scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta*48.0/120);
 }

 // ---------- actions ----------
 void TogglePin(){Pinned=!Pinned;SetPinVisual();Log.Info("Board pinned="+Pinned);}
 void SetPinVisual(){
  pinIcon.Stroke=Pinned?Theme.Primary:Theme.Muted;pinIcon.Fill=Pinned?Theme.Primary:null;pinBtn.SetNormal(Pinned?BoardStyle.Pinned:Brushes.Transparent);
  string t=Pinned?"Unpin (P) \u00B7 close when switching windows":"Pin (P) \u00B7 stay open on top";Theme.Set(pinTipText,t);AutomationProperties.SetName(pinBtn,Pinned?"Unpin":"Pin");
 }
 string refreshResult="";bool refreshFailed;
 void DoRefresh(){
  if(refreshing)return;
  refreshResult="";refreshFailed=false;
  int generation=++refreshGeneration;refreshing=true;refreshDone=false;refreshStarted=DateTime.UtcNow;refreshFullAt=DateTime.MinValue;
  refreshGauge.Visibility=Visibility.Visible;refreshGauge.Set(0.02,Theme.Primary);refreshTimer.Start();
  Theme.Set(refreshTipText,"Refreshing data…");AutomationProperties.SetName(refreshBtn,"Refreshing");sig="";Rebuild();
  Action complete=()=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(generation!=refreshGeneration)return;refreshDone=true;
   if(!refreshFailed&&data!=null){var now=DateTime.UtcNow;int pending=data.Current.Providers.Count(p=>p.Enabled&&p.Windows.Any(w=>!UsageMath.LeftPercent(w,now).HasValue));refreshResult=pending>0?"Refresh done · "+pending+" providers have not sent new quota yet":"Provider refresh done";}
  }));};
  if(data==null){complete();return;}
  var d=data;
  System.Threading.Tasks.Task.Run(async()=>{
   try{
    var server=T3Refresh.RefreshAsync(d.Home);
    var kiro=d.Current.Providers.Any(p=>p.Id=="kiro")?Kiro.RefreshAsync(d.Refresh):System.Threading.Tasks.Task.FromResult(0);
    await System.Threading.Tasks.Task.WhenAll(server,kiro);
   }catch(Exception e){Log.Error("Provider refresh",e);refreshFailed=true;refreshResult="Refresh failed: "+e.GetBaseException().Message;}
   finally{d.Refresh(complete);}
  });
 }
 void RefreshTick(){
  double elapsed=(DateTime.UtcNow-refreshStarted).TotalMilliseconds;
  if(refreshDone&&elapsed>=900){
   refreshGauge.Set(1,Theme.Primary);if(refreshFullAt==DateTime.MinValue)refreshFullAt=DateTime.UtcNow;
   if((DateTime.UtcNow-refreshFullAt).TotalMilliseconds<220)return;
   refreshTimer.Stop();refreshGauge.Visibility=Visibility.Collapsed;refreshing=false;Theme.Set(refreshTipText,refreshResult+" · Refresh (R)");AutomationProperties.SetName(refreshBtn,"Refresh");Notice(refreshResult,refreshFailed,null);return;
  }
  // Completion means the server replied and its cache was reloaded. Estimated
  // progress stays below full while waiting; it never loops through a false 100%.
  double progress=refreshDone?elapsed/900:0.02+0.88*(1-Math.Exp(-elapsed/5000));
  refreshGauge.Set(Math.Max(0.02,Math.Min(1,progress)),Theme.Primary);
 }
 public void OpenNativePicker(){
  HideNow();if(tracker!=null&&!tracker.Current.Found){T3Launcher.Launch();return;}if(tracker==null||driver==null)return;var h=tracker.Current.Hwnd;if(h!=IntPtr.Zero)Native.SetForegroundWindow(h);
  driver.RunAsync("native",null,null,null,null).ContinueWith(t=>{string r=t.IsFaulted?"error:"+t.Exception.GetBaseException().Message:t.Result;if(r!="ok")Log.Info("Native picker: "+r);});
 }
}}
