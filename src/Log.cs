using System;
using System.IO;
namespace T3Usage {
internal static class Log {
 static readonly object Gate=new object();
 public static string DirectoryPath {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"t3-usage");}}
 public static void Info(string text){Write(text);}
 public static void Error(string text, Exception ex){Write(text+"\n"+ex.ToString());}
 static void Write(string text){try {lock(Gate){Directory.CreateDirectory(DirectoryPath);string p=Path.Combine(DirectoryPath,"t3usage.log"); if(File.Exists(p)&&new FileInfo(p).Length>=1048576){if(File.Exists(p+".1"))File.Delete(p+".1");File.Move(p,p+".1");}File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" "+text+Environment.NewLine);}}catch{}}
}}
