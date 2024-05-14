using ReactiveUI;
using System.Reactive;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using pynet_gui_repl.DataModel;
using Python.Runtime;
using DynamicData;
using System.Linq;
using System;

namespace pynet_gui_repl.ViewModels;

public class VariablesViewModel : ViewModelBase
{
    private string _cmdLine = string.Empty;
    private string _cmdHist = string.Empty;

    public ObservableCollection<Variable> ListItems { get; }
    public ReactiveCommand<Unit, Unit> PushCommand { get; }
    public string CommandLine
    {
        get { return _cmdLine; }
        set { this.RaiseAndSetIfChanged(ref _cmdLine, value); }
    }
    public string CommandHistory
    {
        get { return _cmdHist; }
        set { this.RaiseAndSetIfChanged(ref _cmdHist, value); }
    }

    public VariablesViewModel(IEnumerable<Variable> items)
    {
        ListItems = new ObservableCollection<Variable>(items);

        PushCommand = ReactiveCommand.Create(
            () => {
                try
                {
                    Program.Scope.Exec("_ = " + CommandLine);
                }
                catch (System.Exception x)
                {
                    Console.WriteLine(x);
                    throw;
                }
                string result = Program.Scope.Get("_")?.ToString() ?? "";
                // string result = Program.Scope.Get("_")?.Repr() ?? "";
                CommandHistory += "\n>>> " + CommandLine;
                if(!CommandLine.EndsWith(';'))
                {
                    CommandHistory += "\n" + result;
                }
                CommandLine = "";
                ListItems.Clear();
                ListItems.AddRange(
                    Program.Scope.Variables().Items().Where(
                        (tuple) => (!tuple[0]?.ToString()?.StartsWith('_') ?? false) && (!tuple[1]?.ToString()?.Contains("module") ?? false)
                    )
                    .Select(
                        (tuple) => new Variable { Name = tuple[0]?.ToString() ?? "", Value = tuple[1]?.ToString() ?? "" }
                    )
                );
            }
        );
    }
}
