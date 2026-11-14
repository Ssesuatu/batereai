using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace T3Usage {
internal sealed class Hotkey:Forms.NativeWindow,IDisposable {
 [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr h,int id);
 readonly Action toggle;
 public Hotkey(Action a){toggle=a;CreateHandle(new Forms.CreateParams{Caption="T3Usage Hotkey"});if(!RegisterHotKey(Handle,711,0x4003,0x55))Log.Info("Ctrl+Alt+U is unavailable (used by another app)");}
 protected override void WndProc(ref Forms.Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==711)try{toggle();}catch(Exception e){Log.Error("Hotkey",e);}base.WndProc(ref m);}
 public void Dispose(){UnregisterHotKey(Handle,711);DestroyHandle();}
}
public static class App {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 [DllImport("kernel32.dll")]static extern bool FreeConsole();
 [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int pid);
 [DllImport("kernel32.dll")]static extern bool IsProcessInJob(IntPtr process,IntPtr job,out bool result);
 [DllImport("kernel32.dll")]static extern IntPtr GetCurrentProcess();
 // A primary started from inside T3 Code (agents, t3usage:// links) lands in T3's job object and is killed when T3 restarts.
 // Relaunch through WMI so the long-lived instance is parented by WmiPrvSE, outside any job. Test sessions are left alone.
 static bool Detach(string[] args){
  try{
   if(args.Contains("--detached")||args.Contains("--quit")||Environment.GetEnvironmentVariable("T3USAGE_SESSION")!=null||Environment.GetEnvironmentVariable("T3USAGE_IPCTEST")!=null)return false;
   bool inJob;if(!IsProcessInJob(GetCurrentProcess(),IntPtr.Zero,out inJob)||!inJob)return false;
   if(InstanceChannel.ElectionExists())return false;
   string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
   string cmd="\""+exe+"\" --detached"+string.Concat(args.Select(a=>" \""+a.Replace("\"","")+"\"").ToArray());
   using(var cls=new System.Management.ManagementClass("Win32_Process")){
    var res=cls.InvokeMethod("Create",new object[]{cmd,Path.GetDirectoryName(exe),null,null});
    if(Convert.ToInt32(res)!=0){Log.Info("Detach failed: WMI code "+res);return false;}
   }
   Log.Info("Relaunched outside T3 job");return true;
  }catch(Exception e){Log.Error("Detach",e);return false;}
 }
 [STAThread]public static int Main(string[] args){
 AppDomain.CurrentDomain.UnhandledException+=(s,e)=>{var ex=e.ExceptionObject as Exception;if(ex!=null)Log.Error("Unhandled",ex);};
 Forms.Application.ThreadException+=(s,e)=>Log.Error("Forms callback",e.Exception);
 System.Threading.Tasks.TaskScheduler.UnobservedTaskException+=(s,e)=>{Log.Error("Task",e.Exception);e.SetObserved();};
 try{if(args.Contains("--check"))return Checks.Run(args.Length>1?args[1]:"checks");if(args.Contains("--selftest"))return SelfTest(args);
 if(Detach(args))return 0;
 using(var channel=new InstanceChannel()){
 bool wantsQuit=args.Any(a=>a.Equals("--quit",StringComparison.OrdinalIgnoreCase));
 bool bg=args.Any(a=>a.Equals("--background",StringComparison.OrdinalIgnoreCase));
 bool wantsOpen=args.Any(a=>a.Equals("-Open",StringComparison.OrdinalIgnoreCase)||a.StartsWith("t3usage:",StringComparison.OrdinalIgnoreCase));
 if(!channel.Primary){AllowSetForegroundWindow(-1);return channel.Send(wantsQuit,!wantsQuit&&!bg);}if(wantsQuit)return 0;
 var open=channel.Open;var quit=channel.Quit;
 Log.Info("Startup PID "+System.Diagnostics.Process.GetCurrentProcess().Id);FreeConsole();var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=(s,e)=>{Log.Error("Dispatcher",e.Exception);e.Handled=true;};
 // Listen before the slow setup (tray, pill, UIA): requests are acknowledged on receipt and run once the dispatcher pumps.
 string ipcEnv=Environment.GetEnvironmentVariable("T3USAGE_IPCTEST")??"";
 bool ipcTest=ipcEnv!=""&&ipcEnv!="0";int ipcOpens=0;
 var ow=ThreadPool.RegisterWaitForSingleObject(open,(s,b)=>{if(ipcTest&&ipcEnv!="1")try{File.WriteAllText(ipcEnv,Interlocked.Increment(ref ipcOpens).ToString());}catch{}channel.Ack.Set();app.Dispatcher.BeginInvoke(new Action(()=>{try{if(ipcTest)return;if(ui!=null)ui.OpenPopover();else pendingOpen=true;}catch(Exception ex){Log.Error("IPC open",ex);}}));},null,-1,false);
 var qw=ThreadPool.RegisterWaitForSingleObject(quit,(s,b)=>{channel.Ack.Set();app.Dispatcher.BeginInvoke(new Action(()=>app.Shutdown()));new Thread(()=>{Thread.Sleep(5000);Log.Info("Quit forced: UI thread still busy");Environment.Exit(0);}){IsBackground=true}.Start();},null,-1,false);
 channel.Listening();
 using(var data=new DataCore())using(var tracker=new T3Window(app.Dispatcher))using(var tray=new Forms.NotifyIcon())using(var hot=new Hotkey(()=>{if(ui is PillWindow)((PillWindow)ui).KeyboardOpen=true;if(ui!=null)ui.TogglePopover();})){
 Log.Info("Tray initialized");var pill=new PillWindow();ui=pill;ui.Bind(data,tracker,new T3Driver(tracker));Log.Info("Pill bound");Kiro.StartBackground(data.Refresh);
 var menu=new Forms.ContextMenuStrip();menu.Items.Add("Open usage",null,(s,e)=>ui.OpenPopover());menu.Items.Add("Refresh",null,(s,e)=>data.Refresh());
 string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
 var startup=new Forms.ToolStripMenuItem("Start with Windows"){CheckOnClick=true};using(var k=Registry.CurrentUser.OpenSubKey(RunKey))startup.Checked=k!=null&&k.GetValue("T3Usage")!=null;
 startup.Click+=(s,e)=>{try{using(var k=Registry.CurrentUser.CreateSubKey(RunKey)){if(startup.Checked)k.SetValue("T3Usage","\""+exe+"\" --background");else k.DeleteValue("T3Usage",false);}}catch(Exception ex){Log.Error("Startup",ex);}};menu.Items.Add(startup);menu.Items.Add("Quit",null,(s,e)=>app.Shutdown());tray.Icon=System.Drawing.Icon.ExtractAssociatedIcon(exe)??System.Drawing.SystemIcons.Information;tray.Text="Batereai";tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>ui.OpenPopover();tray.Visible=true;ui.AttachTray(tray);
 data.Start();tracker.Start();var warmup=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};warmup.Tick+=(s,e)=>{warmup.Stop();Log.Info("Warmup complete");};warmup.Start();if((wantsOpen||pendingOpen)&&!ipcTest)ui.OpenPopover();Log.Info("Started PID "+System.Diagnostics.Process.GetCurrentProcess().Id);app.Run();ow.Unregister(null);qw.Unregister(null);tray.Visible=false;pill.Close();menu.Dispose();Log.Info("Stopped");}
}return 0;
 }catch(Exception e){Log.Error("Fatal",e);Console.Error.WriteLine(e);return 1;}}
 static volatile IUsageUi ui;static volatile bool pendingOpen;
 static int SelfTest(string[] args){string dir=args.Length>1?Path.GetFullPath(args[1]):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"selftest");Directory.CreateDirectory(dir);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
 using(var data=new DataCore())using(var tracker=new T3Window(app.Dispatcher)){
 var snap=data.LoadOnce();PrintSnapshot(snap);Console.WriteLine(DataCore.Serialize(new {lastModels=snap.LastModels,lastModelsMs=data.LastModelsMs}));
 tracker.Start();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};timer.Tick+=(s,e)=>{timer.Stop();var state=tracker.Current;Console.WriteLine(DataCore.Serialize(new {tracker=new {state.Found,state.Visible,hwnd=state.Hwnd.ToInt64(),window=R(state.WindowRect),picker=R(state.PickerRect),state.PickerName,state.ThreadIdFromUrl,state.Dpi}}));if(state.ThreadIdFromUrl!="")data.SetThreadId(state.ThreadIdFromUrl);snap=data.LoadOnce();PrintSnapshot(snap);
 UiSelfTest.Run(dir,snap,state);app.Shutdown();};timer.Start();app.Run();}return UiSelfTest.Errors==0?0:1;}
 static void PrintSnapshot(Snapshot snap){Console.WriteLine(DataCore.Serialize(new {providers=snap.Providers.Select(p=>new {p.Id,p.Driver,p.Label,p.CheckedAtUtc,p.IsStale,models=p.Models.Select(m=>m.Slug),windows=p.Windows.Select(w=>new {w.Id,w.UsedPercent,w.ResetsAtUtc,left=UsageMath.LeftPercent(w,DateTime.UtcNow),reset=Format.ResetLong(w,DateTime.UtcNow)})}),active=snap.Active}));}
 static object R(Rect r){return r.IsEmpty?null:new {x=r.X,y=r.Y,width=r.Width,height=r.Height};}
}}



