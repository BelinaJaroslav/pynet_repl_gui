using pynet_gui_repl.Services;

namespace pynet_gui_repl.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        var service = new VariablesService();
        Variables = new VariablesViewModel(service.GetItems());
    }
    
    public VariablesViewModel Variables { get; }
    
}
