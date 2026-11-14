using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
namespace T3Usage {internal static class T3Launcher {
 static string history;public static void Remember(uint pid){try{using(var p=Process.GetProcessById((int)pid))history=p.MainModule.FileName;}catch{}}
 public static void Launch(){try{string exe=history;string programs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs");
 if((string.IsNullOrEmpty(exe)||!File.Exists(exe))&&Directory.Exists(programs))exe=Directory.GetDirectories(programs,"t3code*").SelectMany(d=>Directory.GetFiles(d,"T3 Code*.exe",SearchOption.AllDirectories)).FirstOrDefault();
 if(string.IsNullOrEmpty(exe)||!File.Exists(exe)){exe=null;foreach(var folder in new[]{Environment.SpecialFolder.StartMenu,Environment.SpecialFolder.CommonStartMenu}){string start=Environment.GetFolderPath(folder);if(Directory.Exists(start)){exe=Directory.GetFiles(start,"*T3*.lnk",SearchOption.AllDirectories).FirstOrDefault();if(exe!=null)break;}}}
 if(exe==null)throw new FileNotFoundException("T3 Code not found");Process.Start(new ProcessStartInfo(exe){UseShellExecute=true});
 }catch(Exception e){Log.Error("Open T3 Code",e);}}
}}
