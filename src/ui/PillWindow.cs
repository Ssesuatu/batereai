using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
namespace T3Usage {
public enum PillState{Loading,Normal,Warning,Low,Awaiting,Stale,NoData}

// Everything the pill, its tooltip and the tray need, computed from one immutable snapshot.
public sealed class PillModel {
 public PillState State=PillState.Loading;public double Fraction=-1;public string Pct="",Aux="";public int? Left;
 public string Title="Batereai",Line="",Reset="",Age="",Tray="Batereai";
 // WARM/CTX segments (v2). Tone: "warm", "cold", "warning", "error" or "".
 public string CacheLabel="CACHE",CacheValue="\u2014",CacheTone="",CacheTitle="Prompt cache",CacheLine="Loading\u2026",CacheNote="";
 public string CtxValue="\u2014",CtxTone="",CtxTitle="Context",CtxLine="Loading\u2026";
 public string Key(){return State+"|"+Fraction+"|"+Pct+"|"+Aux+"|"+Title+"|"+Line+"|"+Reset+"|"+Age+"|"+Tray+"|"+CacheLabel+"|"+CacheValue+"|"+CacheTone+"|"+CacheTitle+"|"+CacheLine+"|"+CacheNote+"|"+CtxValue+"|"+CtxTone+"|"+CtxTitle+"|"+CtxLine;}
 static readonly System.Globalization.CultureInfo Id=System.Globalization.CultureInfo.InvariantCulture;
 // "~4m" above a minute, "0:37" in the last minute; the pill refreshes every second, so the label only changes when it must.
 internal static string Remaining(TimeSpan d){if(d.TotalSeconds<60)return "0:"+Math.Min(59,(int)Math.Ceiling(Math.Max(0,d.TotalSeconds))).ToString("00");int min=(int)Math.Floor(d.TotalMinutes);return min<60?min+"m":min%60==0?(min/60)+"h":(min/60)+"h "+(min%60)+"m";}
 internal static string Tokens(long v){if(v<1000)return v.ToString(System.Globalization.CultureInfo.InvariantCulture);if(v<10000)return (v/1000.0).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"k";if(v<999500)return Math.Round(v/1000.0)+"k";return (v/1000000.0).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"m";}
 void ApplyCache(CacheState c,DateTime now){
  if(c==null){CacheLine="No cache data for this model in this thread yet.";CtxLine="No context data yet.";return;}
  if(c.Running){CacheLabel="WARM";CacheValue="active";CacheTone="warm";CacheTitle="Prompt cache: warm";CacheLine="Refreshed by every request in this turn.";}
  else if(!c.ExpiresAtUtc.HasValue){CacheLine="This provider has not reported cache usage for this model in this thread.";}
  else{
   var left=c.ExpiresAtUtc.Value-now;string at=c.ExpiresAtUtc.Value.ToLocalTime().ToString("HH:mm",Id);string life=c.TtlMs.HasValue?" \u00B7 lifetime "+Remaining(TimeSpan.FromMilliseconds(c.TtlMs.Value)):"";
   if(left>TimeSpan.Zero){CacheLabel="WARM";CacheValue=(c.Estimated?"~":"")+Remaining(left);CacheTone="warm";CacheTitle="Prompt cache: warm";CacheLine=(c.Estimated?"Estimated to expire at ":"Expires at ")+at+life;}
   else{CacheLabel="COLD";CacheValue="expired";CacheTone="cold";CacheTitle="Prompt cache: cold";CacheLine=(c.Estimated?"Estimated to expire at ":"Expires at ")+at+life;}
   CacheNote=c.Estimated?"Estimated. The provider does not report cache lifetime.":"";
  }
  if(!c.ContextUsed.HasValue){CtxLine="No context data for this conversation yet.";return;}
  long used=c.ContextUsed.Value;long? max=c.ContextMax.HasValue&&c.ContextMax.Value>0?c.ContextMax:null;
  CtxValue=Tokens(used);
  if(max.HasValue){int pct=(int)Math.Round(Math.Min(100,used*100.0/max.Value));CtxTone=pct>=90?"error":pct>=80?"warning":"";CtxTitle="Context: "+pct+"% used";CtxLine=used.ToString("N0",Id)+" of "+max.Value.ToString("N0",Id)+" tokens";}
  else{CtxTitle="Context: "+used.ToString("N0",Id)+" tokens";CtxLine="Context window size not reported.";}
 }
 public static PillModel From(Snapshot s,bool loaded,DateTime now){
  var m=new PillModel();
  if(!loaded){m.Aux="loading\u2026";m.Line="Loading usage data\u2026";return m;}
  m.ApplyCache(s.Cache,now);
  m.State=PillState.NoData;m.Pct="\u2014";m.Aux="no quota";
  var a=s.Active;if(a==null){m.Line="No active thread in T3 yet";return m;}
  var p=s.Providers.FirstOrDefault(x=>x.Id==a.ProviderId);
  m.Title=(p==null?a.ProviderId:p.Label)+" \u00B7 "+a.ModelName;
  if(p==null){m.Line="This provider has no quota data in T3";m.Tray="Batereai \u00B7 "+a.ProviderId+" \u00B7 no quota";return m;}
  bool stale=UiText.IsStale(p,now);
  m.Age="Data "+UiText.Age(p.CheckedAtUtc,now)+(stale?" \u00B7 older than 30 minutes":"");
  var u=UsageMath.ModelUsage(p,a.ModelSlug,now);
  m.Tray="Batereai \u00B7 "+p.Label+" \u00B7 ";
  if(u==null){m.Line="Quota not available for this model yet";m.Tray+="no quota";return m;}
  m.Line=u.Window.Label+": "+Format.Left(u);m.Reset=Format.ResetLong(u.Window,now);
  if(u.AwaitingRefresh){m.State=PillState.Awaiting;m.Aux="awaiting";m.Line=u.Window.Label+": reset passed, awaiting new data";m.Tray+="awaiting data";return m;}
  m.Left=u.Left;m.Fraction=u.Left.Value/100.0;m.Pct=u.Left.Value+"%";m.Aux=Format.ResetShort(u.Window,now);m.Tray+=u.Left.Value+"% left";
  m.State=stale?PillState.Stale:UiText.IsLow(u)?PillState.Low:u.Left<=20?PillState.Warning:PillState.Normal;if(stale)m.Aux="stale";
  return m;
 }
}

// The pill itself: a fixed-width ghost button copied from T3's model trigger.
public sealed class PillView:Border {
 public static readonly double PctSlot=Math.Ceiling(Theme.Measure("100%",Theme.TextSm,FontWeights.Medium));
 public static readonly double AuxSlot=Math.Ceiling(new[]{"\u21BB 29d 23h","awaiting","stale","no quota","loading\u2026"}.Max(s=>Theme.Measure(s,Theme.TextSm,FontWeights.Normal)));
 // v2 WARM/CTX segments: fixed slots so the pill never changes width as the countdown ticks.
 public static readonly double LabelSlot=Math.Ceiling(new[]{"WARM","COLD","CACHE","CTX"}.Max(s=>Theme.Measure(s,Theme.Text2xs,FontWeights.SemiBold)));
 public static readonly double CacheValueSlot=Math.Ceiling(new[]{"~59m","~0:59","1h","active","expired","\u2014"}.Max(s=>Theme.Measure(s,Theme.TextSm,FontWeights.Medium)));
 public static readonly double CtxValueSlot=Math.Ceiling(new[]{"999k","9.9k","1.5m","\u2014"}.Max(s=>Theme.Measure(s,Theme.TextSm,FontWeights.Medium)));
 // Each segment: label, 4 gap, value; then 8 gap, a 1 px divider and 8 gap.
 public static readonly double SegmentsWidth=(LabelSlot+4+CacheValueSlot+17)+(LabelSlot+4+CtxValueSlot+17);
 public static readonly double WidthDip=10+SegmentsWidth+20+16+6+PctSlot+6+AuxSlot+10;
 // Alpha 1/255: invisible on the composer glass but keeps the whole pill hit-testable in a layered window.
 static readonly Brush HitBrush=MakeHit();static Brush MakeHit(){var b=new SolidColorBrush(Color.FromArgb(1,0,0,0));b.Freeze();return b;}
 readonly Ring meter=new Ring();readonly TextBlock pct,aux,cacheLabel,cacheValue,ctxLabel,ctxValue;readonly StackPanel segments;readonly Border hideCacheContext,showCacheContext;readonly PillTip cacheTipView=new PillTip(),ctxTipView=new PillTip();
 public event EventHandler CacheContextToggle;
 public bool Compact;
 // Narrow footers drop the reset text first, then WARM/CTX, then the pill.
 public void SetCompact(bool value){SetWidth(value?CompactWidth:WidthDip,false);}
 public void SetWidth(double width){SetWidth(width,false);}
 public void SetWidth(double width,bool cacheContextHidden){Compact=width==CompactWidth;Theme.Show(aux,width==WidthDip);Theme.Show(segments,!cacheContextHidden&&width!=CompactWidth);Theme.Show(hideCacheContext,!cacheContextHidden&&width!=CompactWidth);Theme.Show(showCacheContext,cacheContextHidden&&width!=CompactWidth);}
 public static double Fit(double available,double scale){return Fit(available,scale,false);}
 public static double Fit(double available,double scale,bool cacheContextHidden){if(cacheContextHidden)return available>=RestoreWidth*scale?RestoreWidth:available>=CompactWidth*scale?CompactWidth:0;return available>=WidthDip*scale?WidthDip:available>=CacheWidth*scale?CacheWidth:available>=CompactWidth*scale?CompactWidth:0;}
 public static double CompactWidth{get{return 10+16+6+PctSlot+10;}}
 public static double RestoreWidth{get{return 10+16+16+6+PctSlot+10;}}
 public static double CacheWidth{get{return 10+SegmentsWidth+20+16+6+PctSlot+10;}}
 // The ghost button fills the fixed width (hover box never changes size); content is right aligned so the text keeps
 // T3's 10 px inset + 2 px gap to the model trigger.
 public readonly Border Button;
 static TextBlock Slot(double width,double size,FontWeight weight,Brush fg){var t=Theme.Text("",size,weight,fg);t.TextTrimming=TextTrimming.None;t.Width=width;return t;}
 static FrameworkElement Segment(TextBlock label,TextBlock value,PillTip tip){
  var s=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent};label.Margin=new Thickness(0,1,4,0);s.Children.Add(label);s.Children.Add(value);
  s.Children.Add(new Border{Width=1,Height=14,Margin=new Thickness(8,0,8,0),Background=Theme.Border,VerticalAlignment=VerticalAlignment.Center});
  var t=Chrome.Tip(tip);ToolTipService.SetToolTip(s,t);ToolTipService.SetInitialShowDelay(s,300);ToolTipService.SetPlacement(s,PlacementMode.Top);return s;
 }
 public PillView(){
  Background=Brushes.Transparent;
  var g=new StackPanel{Orientation=Orientation.Horizontal};
  pct=Theme.Text("",Theme.TextSm,FontWeights.Medium,Theme.Foreground80);pct.TextTrimming=TextTrimming.None;pct.Margin=new Thickness(6,0,0,0);
  aux=Theme.Text("",Theme.TextSm,FontWeights.Normal,Theme.Muted);aux.Margin=new Thickness(6,0,0,0);aux.MaxWidth=AuxSlot;
  cacheLabel=Slot(LabelSlot,Theme.Text2xs,FontWeights.SemiBold,Theme.Muted);cacheValue=Slot(CacheValueSlot,Theme.TextSm,FontWeights.Medium,Theme.Foreground80);
  ctxLabel=Slot(LabelSlot,Theme.Text2xs,FontWeights.SemiBold,Theme.Muted);ctxValue=Slot(CtxValueSlot,Theme.TextSm,FontWeights.Medium,Theme.Foreground80);ctxLabel.Text="CTX";
  segments=new StackPanel{Orientation=Orientation.Horizontal};segments.Children.Add(Segment(cacheLabel,cacheValue,cacheTipView));segments.Children.Add(Segment(ctxLabel,ctxValue,ctxTipView));
  hideCacheContext=CacheContextButton("−","Hide WARM and CTX");showCacheContext=CacheContextButton("+","Show WARM and CTX");
  hideCacheContext.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;var h=CacheContextToggle;if(h!=null)h(this,EventArgs.Empty);};
  showCacheContext.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;var h=CacheContextToggle;if(h!=null)h(this,EventArgs.Empty);};
  g.Children.Add(segments);g.Children.Add(hideCacheContext);g.Children.Add(showCacheContext);g.Children.Add(meter);g.Children.Add(pct);g.Children.Add(aux);
  g.HorizontalAlignment=HorizontalAlignment.Right;
  Button=new Border{CornerRadius=new CornerRadius(8),Background=HitBrush,Padding=new Thickness(10,0,10,0),Cursor=Cursors.Arrow,Child=g};
  Child=Button;Theme.Crisp(this);
  // T3 (Chromium) draws composer text with grayscale AA, and a per-pixel-alpha window cannot use ClearType anyway.
  TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);RenderOptions.SetClearTypeHint(this,ClearTypeHint.Auto);
 }
 static Border CacheContextButton(string label,string tip){var t=Theme.Text(label,Theme.TextSm,FontWeights.Medium,Theme.Muted);t.HorizontalAlignment=HorizontalAlignment.Center;t.VerticalAlignment=VerticalAlignment.Center;var b=new Border{Width=16,Height=18,Margin=new Thickness(2,0,2,0),CornerRadius=new CornerRadius(4),Background=Brushes.Transparent,Cursor=Cursors.Hand,Child=t,ToolTip=tip};return b;}
 public void Apply(PillModel m,bool hover,bool open){
  Button.Background=hover||open?Theme.Accent:HitBrush;
  bool numeric=m.Left.HasValue;Theme.Show(pct,numeric);
  // Ring and percent use the board's level scale (Level); stale data greys the ring and shows amber "stale".
  Brush fill=Theme.Foreground80,pb=open?Theme.Foreground:Theme.Foreground80,ab=Theme.Muted;
  if(m.Left.HasValue){fill=Level.Fill(m.Left);pb=fill;}
  switch(m.State){
   case PillState.Stale:fill=Level.Stale;ab=Theme.WarningForeground;break;
   case PillState.Awaiting:case PillState.NoData:case PillState.Loading:pb=Theme.Muted;break;
  }
  meter.Set(m.Fraction,fill);Theme.Set(pct,m.Pct);Theme.Set(pct,pb);Theme.Set(aux,m.Aux);Theme.Set(aux,ab);
  Theme.Set(cacheLabel,m.CacheLabel);Theme.Set(cacheLabel,m.CacheTone=="warm"?Theme.Primary:Theme.Muted);
  Theme.Set(cacheValue,m.CacheValue);Theme.Set(cacheValue,m.CacheTone=="warm"?Theme.Foreground80:Theme.Muted);
  Brush ctx=m.CtxTone=="error"?Theme.ErrorForeground:m.CtxTone=="warning"?Theme.WarningForeground:(Brush)Theme.Muted;
  Theme.Set(ctxLabel,ctx);Theme.Set(ctxValue,m.CtxValue);Theme.Set(ctxValue,m.CtxTone==""?(m.CtxValue=="\u2014"?Theme.Muted:Theme.Foreground80):ctx);
  cacheTipView.Set(m.CacheTitle,m.CacheLine,m.CacheNote);ctxTipView.Set(m.CtxTitle,m.CtxLine,"");
 }
}

// Tooltip body: provider · model, window + left, full reset text, data age.
public sealed class PillTip:StackPanel {
 readonly TextBlock title=Theme.Text("",Theme.TextXs,FontWeights.Medium,Theme.Foreground),line=Theme.Text("",Theme.TextXs,FontWeights.Normal,Theme.Foreground80),reset=Theme.Text("",Theme.TextXs,FontWeights.Normal,Theme.Muted),age=Theme.Text("",Theme.TextXs,FontWeights.Normal,Theme.Muted);
 public PillTip(){foreach(var t in new[]{title,line,reset,age}){t.TextTrimming=TextTrimming.None;t.Margin=new Thickness(0,1,0,1);Children.Add(t);}MaxWidth=320;}
 public void Set(string t,string l,string note){Theme.Set(title,t);Theme.Set(line,l);Theme.Set(reset,note);Theme.Show(reset,note!="");Theme.Show(age,false);}
 public void Apply(PillModel m){Theme.Set(title,m.Title);Theme.Set(line,m.Line);Theme.Set(reset,m.Reset);Theme.Show(reset,m.Reset!="");Theme.Set(age,m.Age);Theme.Show(age,m.Age!="");Theme.Set(age,m.State==PillState.Stale?Theme.WarningForeground:Theme.Muted);}
}

public static class Chrome {
 const string Ns="xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
 static T Parse<T>(string xaml){return (T)XamlReader.Parse(xaml);}
 public static ToolTip Tip(object content){
  var tip=new ToolTip{Content=content,HasDropShadow=false,Placement=PlacementMode.Top,VerticalOffset=-4,FontFamily=Theme.Font};
  tip.Template=Parse<ControlTemplate>("<ControlTemplate "+Ns+" TargetType='ToolTip'><Border Background='#1C1C1E' BorderBrush='#26FFFFFF' BorderThickness='1' CornerRadius='8' Padding='10,6,10,6' SnapsToDevicePixels='True'><ContentPresenter/></Border></ControlTemplate>");
  Theme.Crisp(tip);return tip;
 }
 public static ContextMenu Menu(){
  var menu=new ContextMenu{HasDropShadow=false,FontFamily=Theme.Font,FontSize=Theme.Text13,Foreground=Theme.Foreground,MinWidth=200};
  menu.Template=Parse<ControlTemplate>("<ControlTemplate "+Ns+" TargetType='ContextMenu'><Border Background='#1C1C1E' BorderBrush='#26FFFFFF' BorderThickness='1' CornerRadius='10' Padding='5' SnapsToDevicePixels='True'><ItemsPresenter/></Border></ControlTemplate>");
  Theme.Crisp(menu);return menu;
 }
 static readonly ControlTemplate ItemTemplate=Parse<ControlTemplate>("<ControlTemplate "+Ns+" TargetType='MenuItem'><Border x:Name='Bd' Height='28' CornerRadius='6' Padding='9,0,9,0' Background='Transparent' SnapsToDevicePixels='True'><ContentPresenter ContentSource='Header' VerticalAlignment='Center' RecognizesAccessKey='False'/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Bd' Property='Background' Value='#22FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
 static readonly ControlTemplate SeparatorTemplate=Parse<ControlTemplate>("<ControlTemplate "+Ns+" TargetType='Separator'><Border Height='1' Margin='4,4,4,4' Background='#18FFFFFF'/></ControlTemplate>");
 public static MenuItem Item(string text,Action click){var i=new MenuItem{Header=text,Template=ItemTemplate};i.Click+=(s,e)=>click();return i;}
 public static Separator Line(){return new Separator{Template=SeparatorTemplate};}
}

public sealed class PillWindow:Window,IUsageUi {
 public bool KeyboardOpen;DataCore data;T3Window tracker;T3Driver driver;Board popover;IntPtr handle;
 readonly PillView view=new PillView();readonly PillTip tipView=new PillTip();ToolTip tip;ContextMenu menu;bool cacheContextHidden;
 DispatcherTimer minute,warm,trim;bool loaded,hover,placed;PillModel model=new PillModel();string modelKey="",pickerName="";bool pickerExpanded;int lx,ly,lw,lh;
 System.Windows.Forms.NotifyIcon tray;System.Drawing.Icon trayIcon;string trayKey="";DateTime hiddenAt=DateTime.MinValue;
 // Measurement switches: T3USAGE_RENDER=hw|sw picks the WPF render path, T3USAGE_NOTRIM=1 disables working-set trimming.
 static readonly bool NoTrim=Environment.GetEnvironmentVariable("T3USAGE_NOTRIM")!="0";
 // Software rendering by default: measured 91 vs 110 MB working set (75 vs 99 MB private) with equal CPU and open latency.
 static PillWindow(){UseRenderMode();}
 public static void UseRenderMode(){if(Environment.GetEnvironmentVariable("T3USAGE_RENDER")!="hw")RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;}
 public PillWindow(){
  Title="T3Usage Pill";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=false;
  // Small per-pixel-alpha window: T3's composer is translucent glass, so the pill must show it through, as T3's own ghost buttons do.
  AllowsTransparency=true;Background=Brushes.Transparent;Width=PillView.WidthDip;Height=28;Content=view;FontFamily=Theme.Font;Theme.Crisp(this);
  var b=view.Button;
  view.CacheContextToggle+=(s,e)=>{cacheContextHidden=!cacheContextHidden;Position();ApplyView();};
  tip=Chrome.Tip(tipView);ToolTipService.SetToolTip(b,tip);ToolTipService.SetInitialShowDelay(b,500);ToolTipService.SetBetweenShowDelay(b,200);ToolTipService.SetPlacement(b,PlacementMode.Top);
  b.MouseEnter+=(s,e)=>{hover=true;ApplyView();};b.MouseLeave+=(s,e)=>{hover=false;ApplyView();};
  b.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;TogglePopover();};
  b.MouseRightButtonUp+=(s,e)=>{e.Handled=true;ShowMenu();};
  SourceInitialized+=(s,e)=>{handle=new WindowInteropHelper(this).Handle;T3Window.NoActivate(handle);HwndSource.FromHwnd(handle).AddHook(Hook);};
 }
 IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){
  if(msg==0x21){handled=true;return new IntPtr(3);}// WM_MOUSEACTIVATE -> MA_NOACTIVATE
  if(msg==0x2E0)Dispatcher.BeginInvoke(new Action(()=>{placed=false;Position();}),DispatcherPriority.Send);// WM_DPICHANGED: WPF moved us to its suggested rect
  return IntPtr.Zero;
 }
 public void Bind(DataCore d,T3Window t,T3Driver drv){
  data=d;tracker=t;driver=drv;loaded=data.Current.Providers.Count>0;
  new WindowInteropHelper(this).EnsureHandle();
  popover=new Board(data,tracker,driver);popover.HiddenByUser+=(s,e)=>{hiddenAt=DateTime.UtcNow;ApplyView();ScheduleTrim(15);};
  data.SnapshotChanged+=OnDataThread;tracker.StateChanged+=OnTracker;
  minute=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(1)};
  minute.Tick+=(s,e)=>{data.RefreshActive();Refresh();popover.MinuteTick();};minute.Start();
  warm=new DispatcherTimer(DispatcherPriority.ApplicationIdle){Interval=TimeSpan.FromSeconds(3)};warm.Tick+=(s,e)=>{warm.Stop();try{popover.Prewarm();}catch(Exception ex){Log.Error("Prewarm",ex);}ScheduleTrim(5);};warm.Start();
  Refresh();Position();
 }
 public void AttachTray(System.Windows.Forms.NotifyIcon t){tray=t;tray.MouseUp+=(s,e)=>{if(e.Button==System.Windows.Forms.MouseButtons.Left)TogglePopover();};trayKey="";UpdateTray();}
 void OnDataThread(object s,EventArgs e){if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{loaded=true;Refresh();popover.SnapshotChanged();}));}
 void OnTracker(object s,EventArgs e){
  var state=tracker.Current;
  data.SetThreadId(state.ThreadIdFromUrl);
  // T3 can update the picker before its SQLite watcher reports the model change.
  // Refresh the active row when the displayed selection changes or the menu closes.
  bool selectionChanged=state.PickerName!=pickerName;
  bool closed=pickerExpanded&&!state.PickerExpanded;
  pickerName=state.PickerName;pickerExpanded=state.PickerExpanded;
  if(selectionChanged||closed)data.RefreshActive();
  Position();Refresh();popover.TrackerChanged();
 }
 void Refresh(){
  var m=PillModel.From(data.Current,loaded,DateTime.UtcNow);string k=m.Key();if(k==modelKey){UpdateTray();return;}modelKey=k;model=m;
  ApplyView();tipView.Apply(m);UpdateTray();
 }
 void ApplyView(){bool open=popover!=null&&popover.IsVisible;view.Apply(model,hover,open);ToolTipService.SetIsEnabled(view.Button,!open);if(open&&tip.IsOpen)tip.IsOpen=false;}
 void UpdateTray(){
  if(tray==null)return;int size=Native.SmallIconSize();bool low=model.State==PillState.Low;int? left=model.State==PillState.Stale||model.State==PillState.Normal||model.State==PillState.Warning||low?model.Left:null;
  if(tracker!=null&&!tracker.Current.Found)left=null;string k=size+"|"+left+"|"+low;
  if(k!=trayKey){trayKey=k;var old=trayIcon;try{trayIcon=TrayGlyph.Make(size,left,low);tray.Icon=trayIcon;}catch(Exception e){Log.Error("Tray icon",e);}if(old!=null){Native.DestroyIcon(old.Handle);old.Dispose();}}
  string trayText=tracker!=null&&!tracker.Current.Found?"Batereai · T3 Code is not open":model.Tray;string text=trayText.Length>63?trayText.Substring(0,62)+"\u2026":trayText;if(tray.Text!=text)tray.Text=text;
 }
 Int32Rect PillRect(){return placed?new Int32Rect(lx,ly,lw,lh):Int32Rect.Empty;}
 void Position(){
  var s=tracker.Current;
  if(handle==IntPtr.Zero)return;
  if(!s.Found||!s.Visible||s.PickerRect.IsEmpty){if(IsVisible)Hide();placed=false;return;}
  double scale=(s.Dpi==0?96:s.Dpi)/96.0;
  double available=s.PickerRect.Left-2-s.PillLeftBoundary;
  double desired=PillView.Fit(available,scale,cacheContextHidden);
  if(desired==0){if(IsVisible)Hide();placed=false;return;}view.SetWidth(desired,cacheContextHidden);
  int h=(int)Math.Round(s.PickerRect.Height),w=(int)Math.Round(desired*scale),x=(int)Math.Round(s.PickerRect.Left)-2-w,y=(int)Math.Round(s.PickerRect.Top);
  if(!placed||x!=lx||y!=ly||w!=lw||h!=lh){
   double dw=w/scale,dh=h/scale;if(Width!=dw)Width=dw;if(Height!=dh)Height=dh;
   T3Window.SetWindowPos(handle,IntPtr.Zero,x,y,w,h,Native.NoActivate|Native.NoZOrder);lx=x;ly=y;lw=w;lh=h;placed=true;
  }
  if(!IsVisible)Show();
  T3Window.KeepAbove(handle,s.Hwnd);
 }
 void ShowMenu(){
  if(menu==null){menu=Chrome.Menu();menu.Items.Add(Chrome.Item("Refresh",()=>data.Refresh()));menu.Items.Add(Chrome.Item("Open T3 model picker",()=>popover.OpenNativePicker()));menu.Items.Add(Chrome.Line());}
  menu.Items.Clear();menu.Items.Add(Chrome.Item("Refresh",()=>data.Refresh()));menu.Items.Add(Chrome.Item("Open T3 model picker",()=>popover.OpenNativePicker()));menu.Items.Add(Chrome.Item(cacheContextHidden?"Show WARM and CTX":"Hide WARM and CTX",()=>{cacheContextHidden=!cacheContextHidden;Position();ApplyView();}));menu.Items.Add(Chrome.Line());menu.Items.Add(Chrome.Item("Quit Batereai",()=>Application.Current.Shutdown()));
  if(tip.IsOpen)tip.IsOpen=false;menu.PlacementTarget=view.Button;menu.Placement=PlacementMode.MousePoint;menu.IsOpen=true;
 }
 public void TogglePopover(){
  if(popover==null)return;
  if(popover.IsVisible){KeyboardOpen=false;popover.HideNow();return;}
  if((DateTime.UtcNow-hiddenAt).TotalMilliseconds<300)return;// the click that deactivated it already closed it
  OpenPopover();
 }
 public void OpenPopover(){
  if(popover==null)return;var sw=Stopwatch.StartNew();
  if(trim!=null)trim.Stop();
  popover.KeyboardOpen=KeyboardOpen;KeyboardOpen=false;popover.ShowAt(IsVisible&&placed,PillRect(),sw);ApplyView();
  if(data.Current.Providers.Any(p=>p.Id=="kiro")){var task=System.Threading.Tasks.Task.Run(()=>Kiro.RefreshAsync(data.Refresh));}
 }
 void ScheduleTrim(int seconds){
  if(NoTrim)return;
  if(trim==null){trim=new DispatcherTimer(DispatcherPriority.ApplicationIdle);trim.Tick+=(s,e)=>{trim.Stop();if(popover!=null&&popover.IsVisible)return;System.Threading.ThreadPool.QueueUserWorkItem(o=>Native.TrimWorkingSet());};}
  trim.Stop();trim.Interval=TimeSpan.FromSeconds(seconds);trim.Start();
 }
 protected override void OnClosed(EventArgs e){
  if(minute!=null)minute.Stop();if(warm!=null)warm.Stop();if(trim!=null)trim.Stop();
  if(data!=null)data.SnapshotChanged-=OnDataThread;if(tracker!=null)tracker.StateChanged-=OnTracker;
  if(popover!=null)popover.Close();if(trayIcon!=null){if(tray!=null)tray.Icon=null;Native.DestroyIcon(trayIcon.Handle);trayIcon.Dispose();}
  base.OnClosed(e);
 }
}}
