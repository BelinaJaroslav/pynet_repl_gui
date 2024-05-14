using Avalonia;
using Avalonia.ReactiveUI;
using DynamicData.Kernel;
using KMB.DataSource;
using KMB.DataSource.File;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Python.Deployment;

namespace pynet_gui_repl;

public sealed class Program
{
    private static Py.GILState _gil;
    private static PyModule _scope;

    public static PyModule Scope { get => _scope; set => _scope = value; }

    private static dynamic pd;
    private static dynamic np;
    private static dynamic px;
    private static dynamic pio;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static async Task Main(string[] args)
    {
        // !!! modify this as needed, alternatively move env var this out
        //Environment.SetEnvironmentVariable("PYTHONNET_PYDLL",
            //"C:\\Users\\Jarda\\AppData\\Local\\Programs\\Python\\Python39\\python39.dll");
        
        await Installer.SetupPython();
        Console.WriteLine(Installer.EmbeddedPythonHome);
        await Installer.TryInstallPip();
        await Installer.PipInstallModule("numpy");
        await Installer.PipInstallModule("pandas");
        await Installer.PipInstallModule("scikit-learn");
        await Installer.PipInstallModule("plotly.express");

        Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", "Python37.dll");

        PythonEngine.Initialize();
        _gil = Py.GIL();
        _scope = Py.CreateScope();
        np = Scope.Import("numpy", "np");
        pd = Scope.Import("pandas", "pd");
        px = Scope.Import("plotly.express", "px");
        Scope.Import("plotly.graph_objects", "go");
        pio = Scope.Import("plotly.io", "pio");
        // :) pio.renderers.__setattr__("default", "browser"); //
        Scope.Import("sklearn.linear_model", "skl_lm");
        Scope.Import("sklearn.preprocessing", "skl_prep");
        Scope.Import("sklearn.tree", "skl_tree");
        dynamic clr = Py.Import("clr");
        clr.AddReference("pynet_gui_repl");
        Scope.Import("pynet_gui_repl", "repl");
        
        Scope.Set("load", LoadCeaData);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        
        PythonEngine.Shutdown();
        
        
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    
    public static PyObject LoadSampleData()
    {
        dynamic df = pd.read_csv("Housing.csv");
        var boolmap = new Dictionary<string, int?>{{"yes", 1}, {"no", 0}};
        // nullable int required
        return df.replace(new Dictionary<string, Dictionary<string, int?>>
            {
                ["mainroad"] = boolmap,
                ["guestroom"] = boolmap,
                ["basement"] = boolmap,
                ["hotwaterheating"] = boolmap,
                ["airconditioning"] = boolmap,
                ["prefarea"] = boolmap,
                ["furnishingstatus"] = new Dictionary<string, int?>{
                    {"unfurnished", 0},
                    {"semi-furnished", 1},
                    {"furnished", 2},
                },
            }
        );
    }

    public static PyObject LoadCeaData()
    {

        string? file = null;
        file ??= "../../../pqmain-2.cea";
        using DataSource ds = new FileDataSource(file);
        using var connection = ds.NewConnection();
        using var transaction = ds.BeginTransaction(connection);
        (Quantity[] q, List<float>[] v, Quantity[] q2, List<byte?>[] v2)
            = DataService.ReadPQArchiveToBytearray(ds, connection, transaction);

        try
        {
            var vButUsable = v
                .SelectMany(inner => inner.Select((item, index) => new { item, index }))
                .GroupBy(i => i.index, i => i.item)
                .Select(g => g.ToList())
                .ToList();

            var vButUsable2 = v2
                .SelectMany(inner => inner.Select((item, index) => new { item, index }))
                .GroupBy(i => i.index, i => i.item)
                .Select(g => g.ToList())
                .ToList();

            dynamic df  = pd.DataFrame(vButUsable , null, q .Select(x => x.PropName).ToList());
            dynamic df2 = pd.DataFrame(vButUsable2, null, q2.Select(x => x.PropName).ToList());
            return df.join(df2);

            // TODO there has to be a better way but everything that should work somehow inexplicably crashes without an exception
            // Console.WriteLine("a");
            // var outer = new PyList();
            // Console.WriteLine("b");
            // foreach (var col in v)
            // {
            //     Console.WriteLine("c1");
            //     var inner = new PyList();
            //     foreach(var val in col)
            //     {
            //         inner.Append(new PyFloat(val));
            //     }
            //     Console.WriteLine("c2");
            //     outer.Append(inner);
            //     Console.WriteLine("c3");
            // }
            // Console.WriteLine("d");
            // dynamic data = np.float32(outer);
            // Console.WriteLine("e");
            // return pd.DataFrame(data.T, Py.kw("columns", q.Select(x => x.ToString())));
        }
        catch (PythonException ex)
        {
            try
            {
                var m = ex.Format();
                Console.WriteLine(m);
                throw;
            }
            catch (Exception exception)
            {
                var t = exception.GetType();
                var s = exception.Message;
                Console.WriteLine(s);
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
}

// df = repl.Program.LoadCeaData()
