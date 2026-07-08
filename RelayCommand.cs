using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AutoFocusGraphs {
    internal sealed class RelayCommand : ICommand {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => canExecute?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => execute(parameter);
    }

    internal sealed class AsyncRelayCommand : ICommand {
        private readonly Func<object, Task> execute;
        private readonly Func<object, bool> canExecute;
        private bool isExecuting;

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null) {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) =>
            !isExecuting && (canExecute?.Invoke(parameter) ?? true);

        public async void Execute(object parameter) {
            if (isExecuting) {
                return;
            }

            isExecuting = true;
            RaiseCanExecuteChanged();
            try {
                await execute(parameter).ConfigureAwait(true);
            } finally {
                isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        private static void RaiseCanExecuteChanged() =>
            CommandManager.InvalidateRequerySuggested();
    }
}
