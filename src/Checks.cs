using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
namespace T3Usage {
internal static class Checks {
 static readonly List<object> results=new List<object>();
 static void Assert(bool ok,string name,object detail){results.Add(new {name,ok,detail});if(!ok)throw new Exception(name+" failed");}
 static int failures;
 // A failed group is recorded and the next group still runs, so one run shows every result.
 static void Step(string group,Action body){try{body();}catch(Exception e){failures++;results.Add(new {group,error=e.ToString()});}}
 static void Skip(string name,string reason){results.Add(new {name,ok=true,skipped=true,reason});}
 public static int Run(string directory){Directory.CreateDirectory(directory);failures=0;try{
  Step("debounce",()=>{foreach(int delay in new[]{900,500}){var times=new List<double>();var clock=Stopwatch.StartNew();using(var d=new BoundedRefresh(delay,()=>{lock(times)times.Add(clock.Elapsed.TotalMilliseconds);})){
   for(int i=0;i<25;i++){d.Signal();Thread.Sleep(200);}Thread.Sleep(1200);
   double[] ticks;lock(times)ticks=times.ToArray();double max=ticks.Length==0?99999:ticks[0];for(int i=1;i<ticks.Length;i++)max=Math.Max(max,ticks[i]-ticks[i-1]);Assert(ticks.Length>=4&&max<=1500,"debounce-"+delay,new {count=ticks.Length,maxGapMs=max,ticks});}}});
  Step("pill",()=>Assert(PillView.Fit(PillView.WidthDip+1,1)==PillView.WidthDip&&PillView.Fit(PillView.WidthDip-1,1)==PillView.CacheWidth&&PillView.Fit(PillView.CacheWidth-1,1)==PillView.CompactWidth&&PillView.Fit(PillView.CompactWidth-1,1)==0&&PillView.Fit(PillView.CompactWidth+1,2)==0,"pill-collision-widths",new {full=PillView.WidthDip,cache=PillView.CacheWidth,compact=PillView.CompactWidth,hidden=0}));
  Step("cache",CacheChecks);
  Step("ties",()=>{
   var now=DateTime.UtcNow;var weekly=new UsageWindow("weekly","Weekly","weekly",50,now.AddDays(5),null,false,false);var session=new UsageWindow("session","Session","session",50,now.AddHours(1),null,false,false);
   var provider=new Provider("p","codex","p","#FFFFFF",0,now,new UsageModel[0],new[]{weekly,session});Assert(UsageMath.ModelUsage(provider,null,now).Window.Id=="session","tie-earliest-reset","session");
   var a=new UsageWindow("a","A","session",50,session.ResetsAtUtc,null,false,false);provider=new Provider("p","codex","p","#FFFFFF",0,now,new UsageModel[0],new[]{session,a});Assert(UsageMath.ModelUsage(provider,null,now).Window.Id=="a","tie-id","a");});
  Step("uia",()=>{using(var release=new ManualResetEvent(false)){var sw=Stopwatch.StartNew();bool timeout=false;try{UiaDeadline.Run(()=>{release.WaitOne();return 1;},2500).GetAwaiter().GetResult();}catch(TimeoutException){timeout=true;}
   int recovered=UiaDeadline.Run(()=>42,2500).GetAwaiter().GetResult();double elapsed=sw.Elapsed.TotalMilliseconds;release.Set();Assert(timeout&&recovered==42&&elapsed<3000,"uia-hang-recovery",new {elapsedMs=elapsed,deadlineMs=2500,recovered});}});
  Step("tracker",TrackerRecovery);
  Step("fixture",()=>Fixture(directory));
  Step("ipc",()=>{string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
   string prevSession=Environment.GetEnvironmentVariable("T3USAGE_SESSION"),prevTest=Environment.GetEnvironmentVariable("T3USAGE_IPCTEST");
   try{for(int i=0;i<3;i++){string countFile=Path.GetFullPath(Path.Combine(directory,"ipc-"+i+".count"));if(File.Exists(countFile))File.Delete(countFile);
    Environment.SetEnvironmentVariable("T3USAGE_SESSION","Check"+Guid.NewGuid().ToString("N"));Environment.SetEnvironmentVariable("T3USAGE_IPCTEST",countFile);
    var sw=Stopwatch.StartNew();using(var primary=Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true})){
     while(!InstanceChannel.ElectionExists()&&sw.ElapsedMilliseconds<5000)Thread.Sleep(2);
     int bare=Child(exe,""),bg=Child(exe,"--background"),open=Child(exe,"-Open"),quit=Child(exe,"--quit");
     bool exited=primary.WaitForExit(5000);int opens=0;if(File.Exists(countFile)){opens=int.Parse(File.ReadAllText(countFile).Trim());File.Delete(countFile);}
     Assert(bare==0&&bg==0&&open==0&&quit==0&&opens==2&&exited,"ipc-immediate-"+i,new {bare,bg,open,quit,opens,exited,elapsedMs=sw.Elapsed.TotalMilliseconds});}}}
   finally{Environment.SetEnvironmentVariable("T3USAGE_SESSION",prevSession);Environment.SetEnvironmentVariable("T3USAGE_IPCTEST",prevTest);}});
  return failures==0?0:1;
 }catch(Exception e){results.Add(new {error=e.ToString()});return 1;}finally{File.WriteAllText(Path.Combine(directory,"checks.json"),DataCore.Serialize(results));Console.WriteLine(DataCore.Serialize(results));}}
 // Same cases as T3 Code's derivePromptCacheWarmth table. Row: status, usage updatedAt, reportsCache, expiresAt, ttlMs, completedAt.
 static void CacheChecks(){
  Func<string,DateTime> T=s=>DataCore.Date(s).Value;
  var exact=CacheMath.Warmth(new[]{new[]{"completed","2026-10-30T14:00:05Z","1","2026-10-30T15:00:00Z","3600000",""}},null,null);
  Assert(exact.ExpiresAtUtc==T("2026-10-30T15:00:00Z")&&!exact.Estimated&&exact.TtlMs==3600000,"cache-claude-exact",exact.Key());
  var keep=CacheMath.Warmth(new[]{new[]{"completed","2026-10-30T14:10:00Z","1","","",""},new[]{"completed","2026-10-30T14:00:00Z","1","2026-10-30T15:00:00Z","3600000",""}},null,null);
  Assert(keep.ExpiresAtUtc==T("2026-10-30T15:10:00Z")&&keep.Estimated&&keep.TtlMs==3600000,"cache-keeps-earlier-ttl",keep.Key());
  var codex=CacheMath.Warmth(new[]{new[]{"completed","2026-10-30T14:00:00Z","1","","",""}},84630,258400);
  Assert(codex.ExpiresAtUtc==T("2026-10-30T14:05:00Z")&&codex.Estimated&&codex.ContextUsed==84630,"cache-estimated",codex.Key());
  var totals=CacheMath.Warmth(new[]{new[]{"completed","","1","","","2026-10-30T14:00:00Z"}},null,null);
  Assert(totals.ExpiresAtUtc==T("2026-10-30T14:05:00Z"),"cache-turn-totals",totals.Key());
  var none=CacheMath.Warmth(new[]{new[]{"completed","2026-10-30T14:00:00Z","0","","","2026-10-30T14:00:00Z"}},null,null);
  Assert(!none.ExpiresAtUtc.HasValue&&!none.Running,"cache-not-reported",none.Key());
  var running=CacheMath.Warmth(new[]{new[]{"running","2026-10-30T14:00:00Z","1","","",""}},null,null);
  Assert(running.Running&&running.ExpiresAtUtc.HasValue,"cache-running",running.Key());
  var silent=CacheMath.Warmth(new[]{new[]{"running","","0","","",""}},null,null);
  Assert(!silent.Running&&!silent.ExpiresAtUtc.HasValue,"cache-running-unreported",silent.Key());
  Assert(PillModel.Remaining(TimeSpan.FromSeconds(36.2))=="0:37"&&PillModel.Remaining(TimeSpan.FromSeconds(59.5))=="0:59"&&PillModel.Remaining(TimeSpan.FromSeconds(60))=="1m"&&PillModel.Remaining(TimeSpan.FromMinutes(60))=="1h"&&PillModel.Tokens(84630)=="85k"&&PillModel.Tokens(4500)=="4.5k","cache-labels",null);
 }
 static void TrackerRecovery(){
  var dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;using(var release=new ManualResetEvent(false))using(var tracker=new T3Window(dispatcher)){
   int calls=0;tracker.UiaProbe=()=>{if(Interlocked.Increment(ref calls)==1)release.WaitOne();};var sw=Stopwatch.StartNew();tracker.Start();
   var frame=new System.Windows.Threading.DispatcherFrame();bool nativeDuringHang=false;
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(20)};
   timer.Tick+=(s,e)=>{if(calls==1&&tracker.Current.Found)nativeDuringHang=true;if(tracker.UiaCompletions>0||sw.ElapsedMilliseconds>4000){timer.Stop();frame.Continue=false;}};
   timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);double elapsed=sw.Elapsed.TotalMilliseconds;release.Set();
   // CI runners and sessions on another desktop cannot see a T3 window, so there is nothing to track.
   if(calls==0&&!tracker.Current.Found){Skip("tracker-hang-recovery","No T3 Code window on this desktop");return;}
   Assert(nativeDuringHang&&tracker.UiaCompletions>0&&elapsed<3000,"tracker-hang-recovery",new {elapsedMs=elapsed,nativeDuringHang,calls});
  }
 }
 static int Child(string exe,string args){using(var p=Process.Start(new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(25000))return -1;return p.ExitCode;}}
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open(byte[] path,out IntPtr db);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr cb,IntPtr arg,out IntPtr error);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr db);
 static byte[] Z(string s){return System.Text.Encoding.UTF8.GetBytes(s+"\0");}
 static void Fixture(string dir){string home=Path.GetFullPath(Path.Combine(dir,"fixture"));Directory.CreateDirectory(Path.Combine(home,"userdata"));Directory.CreateDirectory(Path.Combine(home,"caches"));File.WriteAllText(Path.Combine(home,"userdata","settings.json"),"{}");
  string file=Path.Combine(home,"userdata","statev2.sqlite");if(File.Exists(file))File.Delete(file);IntPtr db,err;sqlite3_open(Z(file),out db);try{
   string sql="CREATE TABLE orchestration_v2_projection_threads(thread_id TEXT,title TEXT,provider_instance_id TEXT,updated_at TEXT,payload_json TEXT,deleted_at TEXT,archived_at TEXT);";
   foreach(string id in new[]{"eligible","thread:delegated-task:test","uuid-subagent","archived","deleted"})sql+="INSERT INTO orchestration_v2_projection_threads VALUES('"+id+"','title','codex','2020-01-01','{\"lastVisitedAt\":\"2020-01-01\""+(id=="uuid-subagent"?",\"lineage\":{\"relationshipToParent\":\"subagent\"}":"")+"}',"+(id=="deleted"?"'x'":"NULL")+","+(id=="archived"?"'x'":"NULL")+");";
   if(sqlite3_exec(db,Z(sql),IntPtr.Zero,IntPtr.Zero,out err)!=0)throw new Exception("Fixture SQL failed");
  }finally{sqlite3_close(db);}
  sqlite3_open(Z(file),out db);try{if(sqlite3_exec(db,Z("INSERT INTO orchestration_v2_projection_threads VALUES('url-eligible','url','codex','2019-01-01','{\"lastVisitedAt\":\"2019-01-01\"}',NULL,NULL);"),IntPtr.Zero,IntPtr.Zero,out err)!=0)throw new Exception("URL fixture");}finally{sqlite3_close(db);}
  string previous=Environment.GetEnvironmentVariable("T3CODE_HOME");Environment.SetEnvironmentVariable("T3CODE_HOME",home);try{using(var data=new DataCore()){foreach(string id in new[]{"thread:delegated-task:test","uuid-subagent","archived","deleted","missing"}){data.SetThreadId(id);var snap=data.LoadOnce();Assert(snap.Active!=null&&snap.Active.ThreadId=="eligible","explicit-fallback-"+id,snap.Active);}
   data.SetThreadId("url-eligible");Assert(data.LoadOnce().Active.ThreadId=="url-eligible","url-preference","url-eligible");
   sqlite3_open(Z(file),out db);try{sqlite3_exec(db,Z("UPDATE orchestration_v2_projection_threads SET payload_json='{\"lastVisitedAt\":\"2099-01-01\"}' WHERE thread_id='eligible';"),IntPtr.Zero,IntPtr.Zero,out err);}finally{sqlite3_close(db);}
   Assert(data.LoadOnce().Active.ThreadId=="url-eligible","url-stays-active-when-other-thread-is-newer","url-eligible");data.SetThreadId("");Assert(data.LoadOnce().Active.ThreadId=="eligible","url-cleared-fallback","eligible");
   data.SetProviderEnabled("codex",false);Assert((DataCore.Read(Path.Combine(home,"userdata","settings.json")).GetObj("providerInstances").GetObj("codex").GetBool("enabled"))==false,"provider-toggle-off",false);
   data.SetProviderEnabled("codex",true);Assert((DataCore.Read(Path.Combine(home,"userdata","settings.json")).GetObj("providerInstances").GetObj("codex").GetBool("enabled"))==true&&File.Exists(Path.Combine(home,"userdata","settings.json.t3usage.bak")),"provider-toggle-on-backup",true);
  }}finally{Environment.SetEnvironmentVariable("T3CODE_HOME",previous);}
 }
}
}
