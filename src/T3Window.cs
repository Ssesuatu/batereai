using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
namespace T3Usage {
public sealed class TrackerState {
 public readonly bool Found,Visible,PickerExpanded;public readonly IntPtr Hwnd;public readonly Rect WindowRect,PickerRect;public readonly string PickerName,ThreadIdFromUrl;public readonly uint Dpi;public double PillLeftBoundary;
 public TrackerState(bool found,bool visible,IntPtr h,Rect wr,Rect pr,string name,bool expanded,string thread,uint dpi){Found=found;Visible=visible;Hwnd=h;WindowRect=wr;PickerRect=pr;PickerName=name;PickerExpanded=expanded;ThreadIdFromUrl=thread;Dpi=dpi;}
}
public sealed class T3Window:IDisposable {
 internal delegate bool EnumProc(IntPtr h,IntPtr l);
 delegate void WinEvent(IntPtr hook,uint ev,IntPtr h,int obj,int child,uint thread,uint time);
 [StructLayout(LayoutKind.Sequential)]internal struct RECT {public int L,T,R,B;public Rect ToRect(){return new Rect(L,T,Math.Max(0,R-L),Math.Max(0,B-T));}}
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc p,IntPtr l);
 [DllImport("user32.dll")]internal static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder s,int n);
 [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr mod,WinEvent cb,uint pid,uint thread,uint flags);
 [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr h);
 [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
 [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetLong(IntPtr h,int n);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetLong(IntPtr h,int n,IntPtr value);
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr h,uint cmd);
 public static void NoActivate(IntPtr h){SetLong(h,-20,new IntPtr(GetLong(h,-20).ToInt64()|0x08000080));}
 public static void KeepAbove(IntPtr child,IntPtr target){IntPtr prev=GetWindow(target,3);if(prev==child)return;IntPtr after=prev!=IntPtr.Zero&&(GetLong(prev,-20).ToInt64()&8)==0?prev:IntPtr.Zero;SetWindowPos(child,after,0,0,0,0,0x213);}
 readonly Dispatcher ui;Dispatcher worker;Thread thread;volatile bool disposed;public volatile bool Paused;WinEvent callback;IntPtr foregroundHook;bool forcePublish;readonly List<IntPtr> hooks=new List<IntPtr>();IntPtr hwnd;AutomationElement picker;Rect offset=Rect.Empty;DateTime validAt=DateTime.MinValue;string name="",urlThread="";bool expanded;uint processId;double leftInset;Size validatedSize=Size.Empty;DispatcherTimer timer;
 volatile TrackerState current=new TrackerState(false,false,IntPtr.Zero,Rect.Empty,Rect.Empty,"",false,"",96);
 public TrackerState Current{get{return current;}}public event EventHandler StateChanged;
 public T3Window(Dispatcher dispatcher){ui=dispatcher;}
 public void Start(){thread=new Thread(Loop){IsBackground=true,Name="T3 window events"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
 void Loop(){worker=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(worker));worker.UnhandledException+=(s,e)=>{Log.Error("Tracker dispatcher",e.Exception);e.Handled=true;};callback=OnEvent;foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);timer=new DispatcherTimer(TimeSpan.FromSeconds(1),DispatcherPriority.Background,(s,e)=>Tick(),worker);try{Tick();Dispatcher.Run();}finally{timer.Stop();foreach(var h in hooks)UnhookWinEvent(h);if(foregroundHook!=IntPtr.Zero)UnhookWinEvent(foregroundHook);}}
 IntPtr Find(){var pids=new HashSet<uint>();foreach(string n in new[]{"T3 Code (Nightly)","T3 Code (Beta)","T3 Code (Dev)","T3 Code"})foreach(var p in Process.GetProcessesByName(n)){using(p)pids.Add((uint)p.Id);}IntPtr best=IntPtr.Zero,fg=GetForegroundWindow();long area=0;EnumWindows((h,l)=>{uint pid;GetWindowThreadProcessId(h,out pid);if(!pids.Contains(pid)||!IsWindowVisible(h))return true;var c=new StringBuilder(100);GetClassName(h,c,100);var t=new StringBuilder(512);GetWindowText(h,t,512);if(c.ToString()!="Chrome_WidgetWin_1"||t.Length==0)return true;RECT r;GetWindowRect(h,out r);long a=(long)(r.R-r.L)*(r.B-r.T);if(h==fg)a+=long.MaxValue/2;if(a>area){area=a;best=h;}return true;},IntPtr.Zero);return best;}
 void Attach(IntPtr h){foreach(var hook in hooks)UnhookWinEvent(hook);hooks.Clear();generation++;hwnd=h;document=null;picker=null;offset=Rect.Empty;name="";urlThread="";if(h==IntPtr.Zero)return;GetWindowThreadProcessId(h,out processId);T3Launcher.Remember(processId);foreach(uint ev in new uint[]{0x8004,0x16,0x17,0x8001,0x800B,0x800C,0x8017,0x8018}){var hook=SetWinEventHook(ev,ev,IntPtr.Zero,callback,processId,0,0);if(hook!=IntPtr.Zero)hooks.Add(hook);} }
 void OnEvent(IntPtr hook,uint ev,IntPtr h,int obj,int child,uint tid,uint time){try{
  if(disposed)return;
  if(ev==3){worker.BeginInvoke(new Action(()=>{try{uint pid;GetWindowThreadProcessId(h,out pid);bool isT3=false;try{using(var p=Process.GetProcessById((int)pid))isT3=p.ProcessName.StartsWith("T3 Code",StringComparison.Ordinal);}catch{}if(isT3){var best=Find();if(best!=hwnd)Attach(best);}forcePublish=true;Publish();Validate(true);}catch(Exception e){Log.Error("Foreground",e);}}));return;}
  if(h!=hwnd||(ev>=0x8000&&obj!=0&&ev!=0x8004))return;
  worker.BeginInvoke(new Action(()=>{try{if(ev==0x8001)Attach(IntPtr.Zero);forcePublish=true;Publish();Validate(false);}catch(Exception e){Log.Error("Native event",e);}}),DispatcherPriority.Send);
 }catch(Exception e){Log.Error("WinEvent",e);}}
 void Tick(){if(disposed)return;try{if(hwnd==IntPtr.Zero||!IsWindow(hwnd)){Attach(Find());}Validate(false);}catch(Exception e){Log.Error("Tracker",e);}}
 internal static AutomationElement FindTrigger(AutomationElement root,out Rect rect,out string label){rect=Rect.Empty;label="";AutomationElement best=null;var cr=new CacheRequest();cr.Add(AutomationElement.ClassNameProperty);cr.Add(AutomationElement.BoundingRectangleProperty);cr.Add(AutomationElement.IsOffscreenProperty);cr.Add(AutomationElement.NameProperty);AutomationElementCollection all;using(cr.Activate())all=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));foreach(AutomationElement e in all){string cls=e.Cached.ClassName??"";Rect r=e.Cached.BoundingRectangle;if(!cls.Contains("group/model-trigger")||e.Cached.IsOffscreen||r.IsEmpty||r.Width<4||r.Height<4)continue;if(best==null||r.Left<rect.Left){best=e;rect=r;label=e.Cached.Name;}}return best;}
 sealed class Reading {public AutomationElement Picker,Document,Composer,Blocker;public double LeftBoundary;public Rect Rect;public string Name,Url;public bool Expanded;}
 internal Action UiaProbe;internal volatile int UiaCompletions;AutomationElement document,composer,blocker;bool reading;int generation;
 async void Validate(bool readUrl){
  Publish();if(Paused||reading||hwnd==IntPtr.Zero)return;
  reading=true;bool retry=false;int gen=generation;IntPtr target=hwnd;var cached=picker;var doc=document;var comp=composer;var block=blocker;
  try{
   var result=await UiaDeadline.Run(()=>{
    var probe=UiaProbe;if(probe!=null){probe();return new Reading{Rect=Rect.Empty,Name="probe",Url=""};}
    Rect r=Rect.Empty;string label="";var p=cached;
    try{if(p!=null){r=p.Current.BoundingRectangle;label=p.Current.Name;if(p.Current.IsOffscreen||r.IsEmpty)p=null;}}catch{p=null;}
    var root=AutomationElement.FromHandle(target);
    if(p==null){p=FindTrigger(root,out r,out label);comp=null;block=null;
     if(p!=null){var parent=TreeWalker.ControlViewWalker.GetParent(p);for(int i=0;i<8&&parent!=null;i++,parent=TreeWalker.ControlViewWalker.GetParent(parent)){var bounds=parent.Current.BoundingRectangle;if(bounds.Width>r.Width+30&&bounds.Height>r.Height+5&&bounds.Height<500){comp=parent;break;}}
      if(comp!=null){var cr=new CacheRequest();cr.Add(AutomationElement.BoundingRectangleProperty);cr.Add(AutomationElement.IsOffscreenProperty);AutomationElementCollection buttons;using(cr.Activate())buttons=comp.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));double edge=0;foreach(AutomationElement b in buttons){var br=b.Cached.BoundingRectangle;if(!b.Cached.IsOffscreen&&!br.IsEmpty&&br.Right<=r.Left&&br.Bottom>r.Top&&br.Top<r.Bottom&&br.Right>edge){edge=br.Right;block=b;}}}
     }
    }
    double boundary=double.PositiveInfinity;if(comp!=null){boundary=comp.Current.BoundingRectangle.Left+8;if(block!=null)boundary=Math.Max(boundary,block.Current.BoundingRectangle.Right+4);}
    bool exp=false;object pattern;if(p!=null&&p.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern))exp=((ExpandCollapsePattern)pattern).Current.ExpandCollapseState==ExpandCollapseState.Expanded;
    string url="";
    try{if(doc==null)doc=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
     if(doc!=null&&doc.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)){var match=Regex.Match(((ValuePattern)pattern).Current.Value??"",@"([0-9a-fA-F]{8}-[0-9a-fA-F-]{27})/?$");url=match.Success?match.Groups[1].Value:"";}
    }catch{doc=null;}
    return new Reading{Picker=p,Document=doc,Composer=comp,Blocker=block,LeftBoundary=boundary,Rect=r,Name=label,Url=url,Expanded=exp};
   },2500);
   if(gen!=generation||disposed)return;
   UiaCompletions++;picker=result.Picker;document=result.Document;composer=result.Composer;blocker=result.Blocker;urlThread=result.Url;name=result.Name;expanded=result.Expanded;
   RECT wr;if(result.Picker!=null&&!result.Rect.IsEmpty&&GetWindowRect(target,out wr)){var r=result.Rect;validatedSize=new Size(wr.R-wr.L,wr.B-wr.T);leftInset=result.LeftBoundary-wr.L;offset=new Rect(r.Left-wr.L,r.Top-wr.B,r.Width,r.Height);validAt=DateTime.UtcNow;}
  }catch(Exception e){retry=e is TimeoutException;if(gen==generation){picker=null;document=null;urlThread="";}Log.Error("UIA deadline",e);}
  finally{reading=false;if(!disposed){Publish();if(retry){var retryOperation=worker.BeginInvoke(new Action(()=>Validate(false)),DispatcherPriority.Background);}}}
 }
 void Publish(){RECT rr;bool found=hwnd!=IntPtr.Zero&&GetWindowRect(hwnd,out rr);Rect wr=Rect.Empty,pr=Rect.Empty;bool visible=false;uint dpi=96;if(found){GetWindowRect(hwnd,out rr);wr=rr.ToRect();int cloak;DwmGetWindowAttribute(hwnd,14,out cloak,4);visible=IsWindowVisible(hwnd)&&!IsIconic(hwnd)&&cloak==0;dpi=GetDpiForWindow(hwnd);if(!offset.IsEmpty&&validatedSize==wr.Size&&DateTime.UtcNow-validAt<TimeSpan.FromSeconds(2))pr=new Rect(wr.Left+offset.Left,wr.Bottom+offset.Top,offset.Width,offset.Height);}
 var next=new TrackerState(found,visible,hwnd,wr,pr,name,expanded,urlThread,dpi);next.PillLeftBoundary=wr.IsEmpty?double.PositiveInfinity:wr.Left+leftInset;var old=current;if(!forcePublish&&old.Found==next.Found&&old.Visible==next.Visible&&old.Hwnd==next.Hwnd&&old.WindowRect==next.WindowRect&&old.PickerRect==next.PickerRect&&old.PickerName==next.PickerName&&old.PickerExpanded==next.PickerExpanded&&old.ThreadIdFromUrl==next.ThreadIdFromUrl&&old.Dpi==next.Dpi&&old.PillLeftBoundary==next.PillLeftBoundary)return;forcePublish=false;current=next;Log.Info("Tracker found="+next.Found+" visible="+next.Visible+" picker="+next.PickerRect+" thread="+next.ThreadIdFromUrl);if(!ui.HasShutdownStarted)ui.BeginInvoke(new Action(()=>{try{var h=StateChanged;if(h!=null)h(this,EventArgs.Empty);}catch(Exception e){Log.Error("Tracker UI",e);}}),DispatcherPriority.Send);}
 public void Dispose(){disposed=true;if(worker!=null)worker.BeginInvokeShutdown(DispatcherPriority.Send);if(thread!=null)thread.Join(2000);}
}}


