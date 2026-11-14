using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace T3Usage {
// --selftest renderer: writes PNGs of every pill state, the usage board (live + synthetic), tooltip, menu and tray glyphs.
public static class UiSelfTest {
 [DllImport("user32.dll")]static extern bool PrintWindow(IntPtr h,IntPtr hdc,uint flags);
 public static int Errors;static readonly List<string> written=new List<string>();
 public static void Run(string dir,Snapshot live,TrackerState state){
  written.Clear();PillWindow.UseRenderMode();
  try{PillStates(dir);}catch(Exception e){Fail("pill-states",e);}
  try{PillContext(dir,live,state);}catch(Exception e){Fail("pill-context",e);}
  // Board max height = 85% of the primary work area, as on screen.
  uint pd;var pwa=Native.WorkAreaAt(0,0,out pd);double maxH=Math.Floor(pwa.Height*0.85/((pd==0?96:pd)/96.0));
  try{BoardPng(dir,"board-live.png",live,state.PickerName,maxH,!state.Found);}catch(Exception e){Fail("board-live",e);}
  // User-sized windows: the cards must follow the window (no outer scrolling, lists grow into free room, scale when tiny).
  try{BoardPng(dir,"board-live-1062x911.png",live,state.PickerName,maxH,!state.Found,1062,911);}catch(Exception e){Fail("board-live-1062",e);}
  try{BoardPng(dir,"board-live-1500x700.png",live,state.PickerName,maxH,!state.Found,1500,700);}catch(Exception e){Fail("board-live-1500",e);}
  try{BoardPng(dir,"board-live-1900x700.png",live,state.PickerName,maxH,!state.Found,1900,700);}catch(Exception e){Fail("board-live-1900",e);}
  try{BoardPng(dir,"board-live-800x500.png",live,state.PickerName,maxH,!state.Found,800,500);}catch(Exception e){Fail("board-live-800",e);}
  try{BoardPng(dir,"board-8.png",Synthetic(8,PillState.Normal,"kiro","claude-opus-5.5"),"claude-opus-5.5, High",maxH,false);}catch(Exception e){Fail("board-8",e);}
  try{BoardPng(dir,"board-2.png",Synthetic(2,PillState.Normal),"gpt-6.1-sol, High",maxH,false);}catch(Exception e){Fail("board-2",e);}
  try{BoardPng(dir,"board-stale.png",Synthetic(5,PillState.Stale),"gpt-6.1-sol, High",maxH,false);}catch(Exception e){Fail("board-stale",e);}
  try{BoardPng(dir,"board-low.png",Synthetic(5,PillState.Low),"gpt-6.1-sol, High",maxH,false);}catch(Exception e){Fail("board-low",e);}
  try{BoardPng(dir,"board-t3-closed.png",Synthetic(5,PillState.Normal),"",maxH,true);}catch(Exception e){Fail("board-t3-closed",e);}
  try{BoardPng(dir,"board-levels.png",Levels(),"gpt-6.1-sol, High",maxH,false);}catch(Exception e){Fail("board-levels",e);}
  try{ScalePng(dir,Levels());}catch(Exception e){Fail("board-scale",e);}
  try{ManagerPng(dir);}catch(Exception e){Fail("board-manager",e);}
  // A short work area (480 DIP max) forces the 8-provider grid to scroll vertically.
  try{BoardPng(dir,"board-8-scroll.png",Synthetic(8,PillState.Normal,"kiro","claude-opus-5.5"),"claude-opus-5.5, High",480,false);}catch(Exception e){Fail("board-8-scroll",e);}
  try{TipAndMenu(dir,live);}catch(Exception e){Fail("tooltip/menu",e);}
  try{ExtraStates(dir);Tray(dir);}catch(Exception e){Fail("tray",e);}
  try{Screen(dir,live,state);}catch(Exception e){Fail("board-focus",e);}
  Console.WriteLine(DataCore.Serialize(new{ui=new{pillWidthDip=PillView.WidthDip,pctSlot=PillView.PctSlot,auxSlot=PillView.AuxSlot,files=written.ToArray()}}));
 }
 static void Fail(string what,Exception e){Errors++;Console.WriteLine(DataCore.Serialize(new{uiError=what,message=e.ToString()}));}

 static BitmapSource Render(FrameworkElement e,double w,double h,double dpi){
  e.Measure(new Size(w,h));e.Arrange(new Rect(0,0,w,h));e.UpdateLayout();
  var rtb=new RenderTargetBitmap((int)Math.Ceiling(w*dpi/96),(int)Math.Ceiling(h*dpi/96),dpi,dpi,PixelFormats.Pbgra32);rtb.Render(e);rtb.Freeze();return rtb;
 }
 static void Save(BitmapSource b,string path){var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(path))enc.Save(f);written.Add(Path.GetFileName(path));}

 static void ExtraStates(string dir){
  var model=PillModel.From(Synthetic(5,PillState.Warning),true,DateTime.UtcNow);var v=new PillView{Width=PillView.CompactWidth,Height=28};v.SetCompact(true);v.Apply(model,false,false);
  var host=new Border{Background=Theme.Composer,Padding=new Thickness(16),Child=v};Save(Render(host,PillView.CompactWidth+32,60,96),Path.Combine(dir,"pill-compact.png"));
  // Middle width: WARM/CTX kept, reset text dropped.
  var cm=PillModel.From(WithCache(new CacheState(false,DateTime.UtcNow.AddMinutes(4.2),300000,true,84630,258400)),true,DateTime.UtcNow);var c=new PillView{Width=PillView.CacheWidth,Height=28};c.SetWidth(PillView.CacheWidth);c.Apply(cm,false,false);
  var ch=new Border{Background=Theme.Composer,Padding=new Thickness(16),Child=c};Save(Render(ch,PillView.CacheWidth+32,60,96),Path.Combine(dir,"pill-cache-narrow.png"));
  // Segment tooltips as they render on hover.
  var tips=new StackPanel{Background=Theme.Page};
  foreach(var tm in new[]{cm,PillModel.From(WithCache(new CacheState(false,DateTime.UtcNow.AddMinutes(42),3600000,false,64200,200000)),true,DateTime.UtcNow)}){
   var a=new PillTip();a.Set(tm.CacheTitle,tm.CacheLine,tm.CacheNote);var b=new PillTip();b.Set(tm.CtxTitle,tm.CtxLine,"");
   foreach(var t in new[]{a,b})tips.Children.Add(new Border{Background=Theme.Popover,BorderBrush=Theme.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(10,6,10,6),Margin=new Thickness(16,8,16,8),HorizontalAlignment=HorizontalAlignment.Left,Child=t});
  }
  Theme.Crisp(tips);tips.Measure(new Size(420,1000));Save(Render(tips,420,Math.Ceiling(tips.DesiredSize.Height),96),Path.Combine(dir,"pill-cache-tooltips.png"));
 }
 static Snapshot WithCache(CacheState c){var s=Synthetic(5,PillState.Normal);return new Snapshot(s.Providers,s.Active,s.LastModels,c);}
 // ---------- synthetic data ----------
 static UsageWindow W(string id,string label,string kind,double used,double hours,string detail=null,bool monthly=false){return new UsageWindow(id,label,kind,used,hours==0?(DateTime?)null:DateTime.UtcNow.AddHours(hours),detail,monthly,monthly);}
 static UsageModel M(string slug,string name){return new UsageModel(slug,name,"{}");}
 internal static Snapshot Synthetic(int count,PillState activeState){return Synthetic(count,activeState,"codex","gpt-6.1-sol");}
 internal static Snapshot Synthetic(int count,PillState activeState,string activeProvider,string activeSlug){
  var now=DateTime.UtcNow;var list=new List<Provider>();
  double codexUsed=activeState==PillState.Low?92:activeState==PillState.Warning?85:38;
  var codexWins=new List<UsageWindow>{activeState==PillState.Awaiting?W("primary","Session","session",100,-0.2):W("primary","Session","session",codexUsed,2.23),W("secondary","Weekly","weekly",35,4*24+3)};
  if(activeState==PillState.NoData)codexWins.Clear();
  list.Add(new Provider("codex","codex","Codex","#F5F5F5",0,activeState==PillState.Stale?now.AddMinutes(-47):now.AddMinutes(-3),new[]{M("gpt-6.1-sol","GPT-6.1-Sol"),M("gpt-6-astra","GPT-6-Astra"),M("gpt-6.1-terra","GPT-6.1 Terra"),M("gpt-6-sol","GPT-6-Sol"),M("gpt-6-luna","GPT-6-Luna"),M("gpt-6-mini","GPT-6 Mini"),M("gpt-5.6-sol","GPT-5.6-Sol"),M("gpt-5.6-mini","GPT-5.6 Mini")},codexWins));
  list.Add(new Provider("claudeAgent","claudeAgent","Claude","#D97757",1,now.AddMinutes(-6),new[]{M("claude-opus-5-5","Claude Opus 5.5")},new[]{W("five_hour","5-hour","session",100,-0.5),W("seven_day","Weekly","weekly",12,5*24)}));
  list.Add(new Provider("antigravity-work","antigravity","Antigravity Work","#8C7BD1",2,now.AddMinutes(-2),
   new[]{M("gemini-3.8-flash","Gemini 3.8 Flash"),M("claude-sonnet-4-6","Claude Sonnet 4.6 (Thinking)"),M("claude-opus-4-6-thinking","Claude Opus 4.6 (Thinking)"),M("gpt-oss-120b-medium","GPT-OSS 120B (Medium)")},
   new[]{W("3p-5h","Claude & GPT (shared) \u00B7 5-hour","session",0,0),W("gemini-5h","Gemini \u00B7 5-hour","session",0.27,3.1),W("3p-weekly","Claude & GPT (shared) \u00B7 Weekly","weekly",50.7,48),W("gemini-weekly","Gemini \u00B7 Weekly","weekly",0.13,116)}));
  list.Add(new Provider("antigravity-home","antigravity","Antigravity Home","#8C7BD1",3,now.AddMinutes(-41),
   new[]{M("gemini-4-argon","Gemini 4 Argon"),M("gemini-3.8-flash","Gemini 3.8 Flash"),M("claude-opus-5-5","Claude Opus 5.5"),M("claude-sonnet-5-5","Claude Sonnet 5.5")},
   new[]{W("gemini-5h","Gemini \u00B7 5-hour","session",4.3,3.7),W("argon-5h","Argon \u00B7 5-hour","session",10,1.03),W("3p-weekly","Claude & GPT (shared) \u00B7 Weekly","weekly",100,61.5),W("gemini-weekly","Gemini \u00B7 Weekly","weekly",57.5,86),W("3p-5h","Claude & GPT (shared) \u00B7 5-hour","session",82,0.85),W("argon-weekly","Argon \u00B7 Weekly","weekly",58,86)}));
  list.Add(new Provider("kiro","kiro","Kiro","#9046FF",4,now.AddMinutes(-12),new[]{M("claude-opus-5.5","claude-opus-5.5"),M("claude-sonnet-5.5","claude-sonnet-5.5"),M("gpt-5.6-sol","gpt-5.6-sol"),M("kimi-k3","kimi-k3"),M("claude-haiku-5.5","claude-haiku-5.5"),M("deepseek-v4","deepseek-v4"),M("qwen4-coder","qwen4-coder")},new[]{W("credits","Credits","monthly",37,20*24+3,"370/1,000",true)}));
  for(int i=5;i<count;i++)list.Add(new Provider("opencode"+i,"opencode","OpenCode "+i,"#5B9BBD",i,now.AddMinutes(-i),new[]{M("m"+i,"Model "+i)},new[]{W("w"+i,"Monthly","monthly",i*9,9*24)}));
  var ap=list.FirstOrDefault(p=>p.Id==activeProvider);var am=ap==null?null:ap.Models.FirstOrDefault(m=>m.Slug==activeSlug);var active=new Active("t1","Thread",activeProvider,activeSlug,am==null?activeSlug:am.Name,"[{\"id\":\"reasoningEffort\",\"value\":\"high\"}]","full-access","url");
  var last=new Dictionary<string,string>{{"codex","gpt-6.1-sol"},{"claudeAgent","claude-opus-5-5"},{"antigravity-work","gemini-3.8-flash"},{"antigravity-home","gemini-4-argon"},{"kiro","claude-opus-5.5"},{"opencode6","m6"}};
  return new Snapshot(list.Take(count),active,last);
 }

 // Board v4 colour scale: green (>= 60), yellow (30-59), orange (11-29), red (<= 10), a stale card (grey meters, amber
 // footer) and an awaiting card (reset passed -> "awaiting new data", no 100%). Codex is in use.
 internal static Snapshot Levels(){
  var now=DateTime.UtcNow;var list=new List<Provider>();
  list.Add(new Provider("codex","codex","Codex","#F5F5F5",0,now.AddMinutes(-1),new[]{M("gpt-6.1-sol","GPT-6.1-Sol"),M("gpt-6-astra","GPT-6-Astra"),M("gpt-6.1-terra","GPT-6.1 Terra")},
   new[]{W("primary","Session","session",25,0.32),W("secondary","Weekly","weekly",6,6*24+14)}));
  list.Add(new Provider("claudeAgent","claudeAgent","Claude","#D97757",1,now.AddMinutes(-4),new[]{M("claude-opus-5-5","Claude Opus 5.5"),M("claude-sonnet-5-5","Claude Sonnet 5.5")},
   new[]{W("five_hour","5-hour","session",55,2.6),W("seven_day","Weekly","weekly",38,3*24+5)}));
  list.Add(new Provider("antigravity-home","antigravity","Antigravity Home","#8C7BD1",2,now.AddMinutes(-2),
   new[]{M("gemini-4-argon","Gemini 4 Argon"),M("gemini-3.8-flash","Gemini 3.8 Flash"),M("claude-opus-5-5","Claude Opus 5.5"),M("claude-sonnet-5-5","Claude Sonnet 5.5"),M("gpt-oss-120b-medium","GPT-OSS 120B (Medium)")},
   new[]{W("gemini-5h","Gemini \u00B7 5-hour","session",5,4.3),W("gemini-weekly","Gemini \u00B7 Weekly","weekly",80,3*24+13),W("3p-weekly","Claude & GPT (shared) \u00B7 Weekly","weekly",100,2*24+12)}));
  list.Add(new Provider("kiro","kiro","Kiro","#9046FF",3,now.AddMinutes(-3),new[]{M("claude-opus-5.5","claude-opus-5.5"),M("claude-sonnet-5.5","claude-sonnet-5.5"),M("kimi-k3","kimi-k3")},
   new[]{W("credits","Credits","monthly",94,20*24+5,"650/693",true)}));
  list.Add(new Provider("antigravity-work","antigravity","Antigravity Work","#8C7BD1",4,now.AddMinutes(-52),
   new[]{M("gemini-3.8-flash","Gemini 3.8 Flash"),M("claude-sonnet-4-6","Claude Sonnet 4.6 (Thinking)")},
   new[]{W("gemini-5h","Gemini \u00B7 5-hour","session",20,1.5),W("3p-weekly","Claude & GPT (shared) \u00B7 Weekly","weekly",48,40)}));
  list.Add(new Provider("cursor","cursor","Cursor","#8B8B8B",5,now.AddMinutes(-2),new[]{M("cursor-auto","Auto"),M("gpt-6.1-sol","GPT-6.1-Sol")},
   new[]{W("primary","Session","session",100,-0.3),W("monthly","Monthly","monthly",30,12*24)}));
  var active=new Active("t1","Thread","codex","gpt-6.1-sol","GPT-6.1-Sol","[{\"id\":\"reasoningEffort\",\"value\":\"high\"}]","full-access","url");
  var last=new Dictionary<string,string>{{"claudeAgent","claude-opus-5-5"},{"antigravity-home","gemini-4-argon"},{"kiro","claude-opus-5.5"},{"antigravity-work","gemini-3.8-flash"},{"cursor","cursor-auto"}};
  return new Snapshot(list,active,last);
 }

 // ---------- pill ----------
 static void PillStates(string dir){
  var rows=new StackPanel{Background=Theme.Page};
  var cases=new List<Tuple<string,PillModel,bool,bool>>();
  cases.Add(Tuple.Create("normal",PillModel.From(Synthetic(5,PillState.Normal),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("hover",PillModel.From(Synthetic(5,PillState.Normal),true,DateTime.UtcNow),true,false));
  cases.Add(Tuple.Create("open",PillModel.From(Synthetic(5,PillState.Normal),true,DateTime.UtcNow),false,true));
  cases.Add(Tuple.Create("amber",PillModel.From(Synthetic(5,PillState.Warning),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("low",PillModel.From(Synthetic(5,PillState.Low),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("awaiting",PillModel.From(Synthetic(5,PillState.Awaiting),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("stale",PillModel.From(Synthetic(5,PillState.Stale),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("no data",PillModel.From(Synthetic(5,PillState.NoData),true,DateTime.UtcNow),false,false));
  cases.Add(Tuple.Create("loading",PillModel.From(Synthetic(5,PillState.Normal),false,DateTime.UtcNow),false,false));
  // v2 WARM/CTX states.
  var now=DateTime.UtcNow;
  cases.Add(Tuple.Create("warm claude",PillModel.From(WithCache(new CacheState(false,now.AddMinutes(42.5),3600000,false,64200,200000)),true,now),false,false));
  cases.Add(Tuple.Create("warm estimated",PillModel.From(WithCache(new CacheState(false,now.AddMinutes(4.2),300000,true,84630,258400)),true,now),false,false));
  cases.Add(Tuple.Create("last minute",PillModel.From(WithCache(new CacheState(false,now.AddSeconds(37),300000,true,84630,258400)),true,now),false,false));
  cases.Add(Tuple.Create("cold",PillModel.From(WithCache(new CacheState(false,now.AddMinutes(-3),300000,true,84630,258400)),true,now),false,false));
  cases.Add(Tuple.Create("running",PillModel.From(WithCache(new CacheState(true,null,null,false,120000,200000)),true,now),false,false));
  cases.Add(Tuple.Create("ctx 85%",PillModel.From(WithCache(new CacheState(false,now.AddMinutes(3),300000,true,170000,200000)),true,now),false,false));
  cases.Add(Tuple.Create("ctx 93%",PillModel.From(WithCache(new CacheState(false,now.AddMinutes(3),300000,true,186000,200000)),true,now),false,false));
  cases.Add(Tuple.Create("no cache data",PillModel.From(WithCache(new CacheState(false,null,null,false,null,null)),true,now),false,false));
  foreach(var c in cases){
   var line=new DockPanel{Height=44,Background=Theme.Composer,Margin=new Thickness(0,0,0,1)};
   var name=Theme.Text(c.Item1+" \u2192 "+c.Item2.State,Theme.TextXs,FontWeights.Normal,Theme.Muted);name.Width=150;name.Margin=new Thickness(12,0,0,0);DockPanel.SetDock(name,Dock.Left);line.Children.Add(name);
   var pill=new PillView{Width=PillView.WidthDip,Height=28,HorizontalAlignment=HorizontalAlignment.Left};pill.SetWidth(PillView.WidthDip);pill.Apply(c.Item2,c.Item3,c.Item4);DockPanel.SetDock(pill,Dock.Left);line.Children.Add(pill);
   var trig=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(2,0,0,0),VerticalAlignment=VerticalAlignment.Center};// stand-in for T3's trigger, same tokens
   var sq=new Border{Width=16,Height=16,CornerRadius=new CornerRadius(4),Background=new SolidColorBrush(Color.FromRgb(0x90,0x46,0xFF)),Margin=new Thickness(10,0,6,0)};trig.Children.Add(sq);
   trig.Children.Add(Theme.Text("claude-opus-5.5",Theme.TextSm,FontWeights.Medium,Theme.Foreground80));var hi=Theme.Text(" High",Theme.TextSm,FontWeights.Normal,Theme.Muted);trig.Children.Add(hi);line.Children.Add(trig);
   rows.Children.Add(line);
  }
  Theme.Crisp(rows);double w=150+12+PillView.WidthDip+200,h=cases.Count*45;
  Save(Render(rows,w,h,96),Path.Combine(dir,"pill-states.png"));
  Save(Render(rows,w,h,144),Path.Combine(dir,"pill-states-150.png"));
 }
 // The real T3 window (PrintWindow, works when covered) with the pill composited where the app would put it.
 static void PillContext(string dir,Snapshot live,TrackerState s){
  if(!s.Found||s.PickerRect.IsEmpty){Console.WriteLine(DataCore.Serialize(new{pillContext="T3 picker not found; skipped"}));return;}
  var wr=s.WindowRect;int ww=(int)wr.Width,wh=(int)wr.Height;double scale=s.Dpi/96.0;
  int pw=(int)Math.Round(PillView.WidthDip*scale),ph=(int)Math.Round(s.PickerRect.Height);int px=(int)Math.Round(s.PickerRect.Left)-2-pw,py=(int)Math.Round(s.PickerRect.Top);
  using(var full=new System.Drawing.Bitmap(ww,wh,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
   using(var g=System.Drawing.Graphics.FromImage(full)){IntPtr dc=g.GetHdc();PrintWindow(s.Hwnd,dc,2);g.ReleaseHdc(dc);}
   var pill=new PillView{Width=pw/scale,Height=ph/scale};var host=new Border{Background=Brushes.Transparent,Child=pill};Theme.Crisp(host);pill.Apply(PillModel.From(live,true,DateTime.UtcNow),false,false);
   var bmp=Render(host,pw/scale,ph/scale,s.Dpi);
   int cx=Math.Max(0,px-(int)wr.Left-60),cy=Math.Max(0,py-(int)wr.Top-24),cw=Math.Min(ww-cx,pw+(int)s.PickerRect.Width+140),ch=Math.Min(wh-cy,ph+48);
   using(var crop=full.Clone(new System.Drawing.Rectangle(cx,cy,cw,ch),full.PixelFormat)){
    using(var g=System.Drawing.Graphics.FromImage(crop)){var ms=new MemoryStream();var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));enc.Save(ms);ms.Position=0;using(var pb=new System.Drawing.Bitmap(ms))g.DrawImageUnscaled(pb,px-(int)wr.Left-cx,py-(int)wr.Top-cy);}
    string p=Path.Combine(dir,"pill-context.png");crop.Save(p,System.Drawing.Imaging.ImageFormat.Png);written.Add("pill-context.png");
    using(var z=new System.Drawing.Bitmap(cw*3,ch*3))using(var g=System.Drawing.Graphics.FromImage(z)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(crop,0,0,cw*3,ch*3);z.Save(Path.Combine(dir,"pill-context-zoom.png"),System.Drawing.Imaging.ImageFormat.Png);written.Add("pill-context-zoom.png");}
   }
  }
  Console.WriteLine(DataCore.Serialize(new{pillPlan=new{x=px,y=py,width=pw,height=ph,picker=new{x=s.PickerRect.X,y=s.PickerRect.Y,width=s.PickerRect.Width,height=s.PickerRect.Height},gap=(int)Math.Round(s.PickerRect.Left)-(px+pw),centerError=(py+ph/2.0)-(s.PickerRect.Top+s.PickerRect.Height/2)}}));
 }

 // ---------- board ----------
 static void BoardPng(string dir,string file,Snapshot snap,string picker,double maxH,bool closed){
  var board=new Board(null,null,null){FixedSnapshot=snap,FixedPickerName=picker,FixedClosed=closed};board.UseDrawnChrome(true);
  BoardPng(dir,file,snap,picker,maxH,closed,0,0);
 }
 // fixedW/fixedH > 0: a window the user sized; otherwise the best-fit window within the primary work area (maxH tall at most).
 static void BoardPng(string dir,string file,Snapshot snap,string picker,double maxH,bool closed,double fixedW,double fixedH){
  var board=new Board(null,null,null){FixedSnapshot=snap,FixedPickerName=picker,FixedClosed=closed};board.UseDrawnChrome(true);
  uint pd;var pwa=Native.WorkAreaAt(0,0,out pd);double maxW=Math.Floor((pwa.Width-16)/((pd==0?96:pd)/96.0));
  var sw=Stopwatch.StartNew();board.Rebuild();double buildMs=sw.Elapsed.TotalMilliseconds;
  double w,h;if(fixedW>0){board.LayoutForTest(fixedW,fixedH);w=fixedW;h=fixedH;}else{var size=board.FitForTest(maxW,maxH);w=size.Width;h=size.Height;}
  double natural=h;double layoutMs=sw.Elapsed.TotalMilliseconds;
  var v=(FrameworkElement)board.Visual;board.Content=null;
  var host=new Border{Background=Theme.Page,Padding=new Thickness(16),Child=v};Theme.Crisp(host);
  Save(Render(host,w+32,h+32,96),Path.Combine(dir,file));
  // Layout facts: cards per row, card heights per row (must be equal), clipped text, cards whose rows scroll.
  var cards=new List<Clickable>();Collect(v,cards);var outer=FindScroll(v);
  var perRow=cards.Where(c=>c.Key.StartsWith("card:")).GroupBy(c=>Math.Round(c.TranslatePoint(new Point(0,0),v).Y)).OrderBy(g=>g.Key).Select(g=>new{count=g.Count(),heights=g.Select(c=>Math.Round(c.ActualHeight,2)).Distinct().ToArray(),width=g.Select(c=>c.ActualWidth).Distinct().ToArray()}).ToArray();
  int unequal=perRow.Count(r=>r.heights.Length>1);
  var inner=new List<ScrollViewer>();CollectScroll(v,inner);inner.Remove(outer);
  var clipped=new List<string>();CollectClipped(v,clipped);
  if(unequal>0)Errors++;
  Console.WriteLine(DataCore.Serialize(new{board=file,widthDip=w,columns=board.Columns,scale=board.FitScale,heightDip=h,naturalHeightDip=natural,maxHeightDip=maxH,gridScrolls=natural>maxH,gridExtent=outer==null?0:outer.ExtentHeight,gridViewport=outer==null?0:outer.ViewportHeight,rows=perRow,unequalRows=unequal,
   scrollingCards=inner.Select(s=>new{extent=s.ExtentHeight,viewport=s.ViewportHeight,scrollable=s.ScrollableHeight}).ToArray(),trimmedTexts=clipped.ToArray(),providers=snap.Providers.Count,rebuildMs=buildMs,rebuildAndMeasureMs=layoutMs}));
  board.Close();
 }
 static void ScalePng(string dir,Snapshot snap){
   foreach(double k in new[]{1.0,0.7,0.45}){
    var board=new Board(null,null,null){FixedSnapshot=snap,FixedPickerName="gpt-6.1-sol, High"};board.UseDrawnChrome(true);board.Rebuild();
    var size=board.FitForTest(1900,1000);double nh=size.Height,w=size.Width;var v=(FrameworkElement)board.Visual;board.Content=null;
    var host=new Border{Background=Theme.Page,Padding=new Thickness(16),Child=v};Theme.Crisp(host);
    Save(Render(host,w*k+32,nh*k+32,96),Path.Combine(dir,"board-scale-"+(int)Math.Round(k*100)+".png"));board.Close();
   }
  }
  // "Manage providers" popover: Cursor and the stale Antigravity account are off.
  static void ManagerPng(string dir){
   var snap=Levels();foreach(var p in snap.Providers)if(p.Id=="cursor"||p.Id=="antigravity-work")p.Enabled=false;
   var board=new Board(null,null,null){FixedSnapshot=snap,FixedPickerName="gpt-6.1-sol, High"};board.Rebuild();
   var v=board.ManagerVisual();var host=new Border{Background=Theme.Page,Padding=new Thickness(8),Child=v};Theme.Crisp(host);
   host.Measure(new Size(400,1000));Save(Render(host,Math.Ceiling(host.DesiredSize.Width),Math.Ceiling(host.DesiredSize.Height),96),Path.Combine(dir,"board-manager.png"));board.Close();
  }
  static void Collect(DependencyObject d,List<Clickable> list){var c=d as Clickable;if(c!=null)list.Add(c);for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)Collect(VisualTreeHelper.GetChild(d,i),list);}
 static void CollectScroll(DependencyObject d,List<ScrollViewer> list){var c=d as ScrollViewer;if(c!=null)list.Add(c);for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)CollectScroll(VisualTreeHelper.GetChild(d,i),list);}
 // TextBlocks whose text is wider than their slot (ellipsis shown).
 static void CollectClipped(DependencyObject d,List<string> list){
  var fe=d as UIElement;if(fe!=null&&fe.Visibility!=Visibility.Visible)return;
  // Wrapping text (the card footer) takes a second line instead of clipping, so it is not counted.
  var t=d as TextBlock;if(t!=null&&t.ActualWidth>0&&t.Text!=""&&t.TextWrapping==TextWrapping.NoWrap){
   // Same text, font and rendering mode, unconstrained: wider than the laid-out block means trimmed or clipped.
   var probe=new TextBlock{Text=t.Text,FontFamily=t.FontFamily,FontSize=t.FontSize,FontWeight=t.Inlines.Count>1?FontWeights.SemiBold:t.FontWeight};Theme.Crisp(probe);System.Windows.Documents.Typography.SetNumeralAlignment(probe,FontNumeralAlignment.Tabular);
   probe.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));double need=probe.DesiredSize.Width;if(need>t.ActualWidth+0.5)list.Add(t.Text+" ("+Math.Round(need,1)+">"+Math.Round(t.ActualWidth,1)+")");}
  for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)CollectClipped(VisualTreeHelper.GetChild(d,i),list);
 }
 static ScrollViewer FindScroll(DependencyObject d){if(d is ScrollViewer)return (ScrollViewer)d;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var r=FindScroll(VisualTreeHelper.GetChild(d,i));if(r!=null)return r;}return null;} static void TipAndMenu(string dir,Snapshot live){
  var tipView=new PillTip();tipView.Apply(PillModel.From(live,true,DateTime.UtcNow));
  var tipBorder=new Border{Background=Theme.Popover,BorderBrush=Theme.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(8,5,8,5),Child=tipView,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
  var host=new Border{Background=Theme.Page,Padding=new Thickness(16),Child=tipBorder};Theme.Crisp(host);host.Measure(new Size(400,300));
  Save(Render(host,Math.Ceiling(host.DesiredSize.Width),Math.Ceiling(host.DesiredSize.Height),96),Path.Combine(dir,"tooltip.png"));
  var menu=Chrome.Menu();menu.Items.Add(Chrome.Item("Refresh",()=>{}));var hi=Chrome.Item("Open T3 model picker",()=>{});menu.Items.Add(hi);menu.Items.Add(Chrome.Line());menu.Items.Add(Chrome.Item("Quit Batereai",()=>{}));
  // A ContextMenu cannot be parented, so render it alone and composite it onto the page colour.
  menu.Measure(new Size(400,300));menu.Arrange(new Rect(menu.DesiredSize));menu.UpdateLayout();
  var bd=FindBd(hi);if(bd!=null)bd.Background=Theme.RowHighlight;// show the highlighted state
  var mb=Render(menu,Math.Ceiling(menu.DesiredSize.Width),Math.Ceiling(menu.DesiredSize.Height),96);
  var mh=new Border{Background=Theme.Page,Padding=new Thickness(16),Child=new Image{Source=mb,Width=mb.PixelWidth,Height=mb.PixelHeight}};
  Save(Render(mh,mb.PixelWidth+32,mb.PixelHeight+32,96),Path.Combine(dir,"menu.png"));
 }
 static Border FindBd(DependencyObject d){if(d is Border&&((Border)d).Name=="Bd")return (Border)d;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++){var r=FindBd(VisualTreeHelper.GetChild(d,i));if(r!=null)return r;}return null;}
 static void Tray(string dir){
  int[] sizes={16,20,24,32};int[] lefts={100,64,8};int W=0;foreach(var s in sizes)W+=s*4+8;
  using(var sheet=new System.Drawing.Bitmap(W*4/4+8,(32+8)*lefts.Length*4))using(var g=System.Drawing.Graphics.FromImage(sheet)){
   g.Clear(System.Drawing.Color.FromArgb(255,32,32,32));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
   int y=4;foreach(var l in lefts){int x=4;foreach(var s in sizes){using(var ic=TrayGlyph.Make(s,l,l<=10))using(var b=ic.ToBitmap()){g.DrawImage(b,x,y,s*4,s*4);Native.DestroyIcon(ic.Handle);}x+=s*4+8;}y+=32*4+8;}
   sheet.Save(Path.Combine(dir,"tray-icons.png"),System.Drawing.Imaging.ImageFormat.Png);written.Add("tray-icons.png");
  }
 }
 // A real top-level board on screen (DWM corners/border, real keyboard focus), captured from the screen.
 static void Screen(string dir,Snapshot live,TrackerState s){
  var board=new Board(null,null,null){FixedSnapshot=live,FixedPickerName=s.PickerName,FixedClosed=!s.Found,NoActivateForTest=false,KeyboardOpen=true};
  var cold=Stopwatch.StartNew();board.ShowAt(false,Int32Rect.Empty,cold);double coldMs=cold.Elapsed.TotalMilliseconds;Pump(450);
  board.HideNow();Pump(100);
  var warm=Stopwatch.StartNew();board.ShowAt(false,Int32Rect.Empty,warm);double warmMs=warm.Elapsed.TotalMilliseconds;Pump(450);
  var focused=System.Windows.Input.Keyboard.FocusedElement as Clickable;bool initial=focused!=null&&focused.IsKeyboardFocused;string initialName=focused==null?null:System.Windows.Automation.AutomationProperties.GetName(focused);
  var h=new System.Windows.Interop.WindowInteropHelper(board).Handle;T3Window.RECT r;T3Window.GetWindowRect(h,out r);
  int m=12,x=r.L-m,y=r.T-m,w=r.R-r.L+2*m,hh=r.B-r.T+2*m;
  CaptureOrRender(board,x,y,w,hh,Path.Combine(dir,"board-focus.png"));
  // Keys on our own window only, never on T3: Right moves focus, P pins, Esc closes.
  Press(board,System.Windows.Input.Key.Right);Pump(50);var after=System.Windows.Input.Keyboard.FocusedElement as Clickable;bool moved=after!=null&&after!=focused;
  Press(board,System.Windows.Input.Key.P);bool pinned=board.Pinned;
  var focusHost=new Window{Width=100,Height=60,Left=0,Top=0,ShowInTaskbar=false,Title="Usage focus check"};focusHost.Show();focusHost.Activate();Pump(250);
  bool pinnedInactive=board.IsVisible&&!board.IsActive;
  CaptureOrRender(board,x,y,w,hh,Path.Combine(dir,"board-pinned-inactive.png"));
  focusHost.Close();board.Activate();Pump(80);
  if(!pinnedInactive)Errors++;
  Press(board,System.Windows.Input.Key.P);bool unpinned=!board.Pinned;
  Press(board,System.Windows.Input.Key.Escape);bool escClosed=!board.IsVisible;
  if(!initial||!moved||!pinned||!unpinned||!escClosed)Errors++;
  Console.WriteLine(DataCore.Serialize(new{boardScreen=new{x=r.L,y=r.T,width=r.R-r.L,height=r.B-r.T,coldShowCallMs=coldMs,warmShowCallMs=warmMs,lastRebuildMs=board.LastBuildMs},keyboard=new{initialFocus=initial,name=initialName,rightMoved=moved,movedTo=after==null?null:System.Windows.Automation.AutomationProperties.GetName(after),pinToggle=pinned&&unpinned,escClosed}}));
  board.Close();
 }
 static void CaptureOrRender(Board board,int x,int y,int w,int hh,string path){
  try{using(var b=new System.Drawing.Bitmap(w,hh))using(var g=System.Drawing.Graphics.FromImage(b)){g.CopyFromScreen(x,y,0,0,new System.Drawing.Size(w,hh));b.Save(path,System.Drawing.Imaging.ImageFormat.Png);written.Add(Path.GetFileName(path));}}
  catch{Save(Render(board,Math.Max(1,board.ActualWidth),Math.Max(1,board.ActualHeight),96),path);}
 }
 static void Press(Window w,System.Windows.Input.Key k){var e=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),Environment.TickCount,k){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};w.RaiseEvent(e);} static void Pump(int ms){var frame=new DispatcherFrame();var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};t.Tick+=(o,e)=>{t.Stop();frame.Continue=false;};t.Start();Dispatcher.PushFrame(frame);}
}}
