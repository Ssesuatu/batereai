using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
namespace T3Usage {
internal sealed class InstanceChannel:IDisposable {
 static string Prefix {get{return @"Local\T3UsageEngine"+(Environment.GetEnvironmentVariable("T3USAGE_SESSION")??"");}}
 public static bool ElectionExists(){Mutex m;if(!Mutex.TryOpenExisting(Prefix,out m))return false;m.Dispose();return true;}
 public readonly EventWaitHandle Open,Quit,Ready,Ack;
 readonly Mutex owner;public readonly bool Primary;
 string PidFile {get{return Path.Combine(Log.DirectoryPath,"primary"+(Environment.GetEnvironmentVariable("T3USAGE_SESSION")??"")+".pid");}}
 static EventWaitHandle Existing(string name){var sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<5000){try{return EventWaitHandle.OpenExisting(name);}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(20);}}throw new TimeoutException("IPC event "+name);}
 public InstanceChannel(){Mutex existing;bool hasOwner=Mutex.TryOpenExisting(Prefix,out existing);
  if(hasOwner){owner=existing;try{Primary=owner.WaitOne(0);}catch(AbandonedMutexException){Primary=true;}
   if(!Primary){Open=Existing(Prefix+"Open");Quit=Existing(Prefix+"Quit");Ready=Existing(Prefix+"Ready");Ack=Existing(Prefix+"Ack");return;}
  }
  // New primaries create and retain the channels before exposing the ownership mutex.
  Open=new EventWaitHandle(false,EventResetMode.AutoReset,Prefix+"Open");
  Quit=new EventWaitHandle(false,EventResetMode.AutoReset,Prefix+"Quit");
  Ready=new EventWaitHandle(false,EventResetMode.ManualReset,Prefix+"Ready");
  Ack=new EventWaitHandle(false,EventResetMode.AutoReset,Prefix+"Ack");
  if(!hasOwner){owner=new Mutex(false,Prefix);try{Primary=owner.WaitOne(0);}catch(AbandonedMutexException){Primary=true;}}
  if(Primary){Ready.Reset();Open.Reset();Quit.Reset();Ack.Reset();}
 }
 public void Listening(){Directory.CreateDirectory(Log.DirectoryPath);File.WriteAllText(PidFile,Process.GetCurrentProcess().Id.ToString());Ready.Set();}
 public int Send(bool quit,bool open){
  if(!quit&&!open)return 0;
  try{using(var sender=new Mutex(false,Prefix+"Sender")){
   bool acquired=false;try{try{acquired=sender.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new TimeoutException("IPC sender");
   if(!Ready.WaitOne(15000))throw new TimeoutException("IPC ready");
   Process primary=null;if(quit)primary=Process.GetProcessById(int.Parse(File.ReadAllText(PidFile)));
   using(primary){Ack.Reset();(quit?Quit:Open).Set();if(!Ack.WaitOne(5000))throw new TimeoutException("IPC acknowledgement");
    if(primary!=null&&!primary.WaitForExit(15000))throw new TimeoutException("Primary exit");}
   Log.Info("IPC acknowledged "+(quit?"quit":"open"));return 0;
   }finally{if(acquired)sender.ReleaseMutex();}
  }}catch(Exception e){Log.Error("IPC failed",e);return 2;}
 }
 public void Dispose(){if(Primary){Ready.Reset();owner.ReleaseMutex();}owner.Dispose();Open.Dispose();Quit.Dispose();Ready.Dispose();Ack.Dispose();}
}
}
