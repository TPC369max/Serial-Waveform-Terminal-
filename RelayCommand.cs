using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Yell
{
    public class RelayCommand : ICommand
    {
        public readonly Action _Execute;
        public RelayCommand(Action execute)=>_Execute=execute;
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _Execute();
    }
}
