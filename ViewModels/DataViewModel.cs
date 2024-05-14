using ReactiveUI;
using System.Reactive;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using pynet_gui_repl.DataModel;
using Python.Runtime;
using DynamicData;
using System.Linq;

namespace pynet_gui_repl.ViewModels;

public class DataViewModel : ViewModelBase
{
    public ObservableCollection<Variable> ListItems { get; }

    public DataViewModel(IEnumerable<Variable> items)
    {

    }
}