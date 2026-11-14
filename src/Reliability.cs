using System;
using System.Threading;
using System.Threading.Tasks;
namespace T3Usage {
// A dedicated MTA lane owns UIA calls; supervision runs independently of that lane.
// A stuck COM call cannot be killed safely. Abandon its lane, discard late results,
// and use a fresh worker. Queued calls on an abandoned lane never execute.
internal static class UiaDeadline {
 sealed class Lane {
  public volatile bool Abandoned;public readonly System.Collections.Concurrent.BlockingCollection<Action> Queue=new System.Collections.Concurrent.BlockingCollection<Action>();
  public Lane(){var thread=new Thread(()=>{try{foreach(var call in Queue.GetConsumingEnumerable()){if(Abandoned)break;call();}}catch(Exception e){Log.Error("UIA worker",e);}}){IsBackground=true,Name="T3 UIA worker"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
 }
 static readonly object gate=new object();static Lane current;
 public static async Task<T> Run<T>(Func<T> call,int milliseconds){
  var done=new TaskCompletionSource<T>();Lane lane;
  lock(gate){lane=current??(current=new Lane());lane.Queue.Add(()=>{try{done.TrySetResult(call());}catch(Exception e){done.TrySetException(e);}});}
  if(await Task.WhenAny(done.Task,Task.Delay(milliseconds)).ConfigureAwait(false)!=done.Task){
   lock(gate){if(current==lane){lane.Abandoned=true;lane.Queue.CompleteAdding();current=new Lane();}}
   // Observe a late failure without allowing it to affect a newer request.
   var observation=done.Task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
   throw new TimeoutException("UI Automation exceeded "+milliseconds+" ms");
  }
  return await done.Task.ConfigureAwait(false);
 }
}
// Leading deadline: later events mark dirty, but never move the scheduled deadline.
internal sealed class BoundedRefresh:IDisposable {
 readonly object gate=new object();readonly Timer timer;readonly Action action;readonly int delay;
 bool pending,running,dirty,disposed;
 public BoundedRefresh(int ms,Action a){delay=ms;action=a;timer=new Timer(Run,null,-1,-1);}
 public void Signal(){lock(gate){if(disposed)return;if(pending||running){dirty=true;return;}pending=true;timer.Change(delay,-1);}}
 void Run(object unused){lock(gate){if(disposed)return;pending=false;running=true;dirty=false;}try{action();}catch(Exception e){Log.Error("Refresh callback",e);}finally{lock(gate){running=false;if(dirty&&!disposed){pending=true;timer.Change(delay,-1);}}}}
 public void Dispose(){lock(gate){disposed=true;timer.Dispose();}}
}
}
