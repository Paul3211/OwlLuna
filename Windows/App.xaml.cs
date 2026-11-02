using System.IO;
using System.Windows;

namespace OwlLuna.Windows;
public partial class App : Application
{
    Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 3 && e.Args[0] == "--configure-google")
        {
            try { new GoogleDrive(e.Args[2]).Configure(e.Args[1]); Shutdown(0); }
            catch { Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            try { SelfTests.Run(e.Args.SkipWhile(a => a != "--self-test").Skip(1).FirstOrDefault()); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var rootIndex=Array.IndexOf(e.Args,"--data-root");
        var root=rootIndex>=0&&rootIndex+1<e.Args.Length?Path.GetFullPath(e.Args[rootIndex+1]):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OwlLunaWindows");
        instance=new Mutex(true,"Local\\OwlLuna-"+Json.Hash(System.Text.Encoding.UTF8.GetBytes(root.ToUpperInvariant()))[..24],out var created);
        if(!created){MessageBox.Show("Diese OwlLuna-Bibliothek ist bereits geöffnet.","OwlLuna");instance.Dispose();instance=null;Shutdown();return;}
        DispatcherUnhandledException += (_, args) => {
            MessageBox.Show("Die Aktion konnte nicht abgeschlossen werden.\n\n" + args.Exception.Message, "OwlLuna");
            args.Handled = true;
        };
        new MainWindow().Show();
    }
    protected override void OnExit(ExitEventArgs e){instance?.ReleaseMutex();instance?.Dispose();base.OnExit(e);}
}
