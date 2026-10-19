using System;
using System.Windows;

namespace HaveRestwin;

/// 单台显示器上的全屏休息遮罩。内容全部绑定 RestOverlayController。
public partial class RestOverlayWindow : Window
{
    public RestOverlayWindow(RestOverlayController controller)
    {
        InitializeComponent();
        DataContext = controller;
    }
}

/// 简易 ICommand 包装。
public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> execute;

    public RelayCommand(Action<object?> execute) => this.execute = execute;

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}
