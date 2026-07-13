using System.Windows;
using System.Windows.Controls;
using BuptGpaCalculator.Core.Models;

namespace BuptGpaCalculator.App.Dialogs;

/// <summary>Collects the minimal profile required for a student archive entry.</summary>
public sealed class StudentDialog : Window
{
    private readonly TextBox nameTextBox = new();
    private readonly TextBox studentIdTextBox = new();

    /// <summary>Initializes the dialog.</summary>
    public StudentDialog()
    {
        Title = "新增学生"; Width = 360; Height = 210; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new Grid { Margin = new Thickness(20) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) }); panel.ColumnDefinitions.Add(new ColumnDefinition());
        AddField(panel, "姓名", nameTextBox, 0); AddField(panel, "学号", studentIdTextBox, 1);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "取消", IsCancel = true }; var confirm = new Button { Content = "确定", IsDefault = true };
        confirm.Click += Confirm_Click; buttons.Children.Add(cancel); buttons.Children.Add(confirm); Grid.SetRow(buttons, 2); Grid.SetColumnSpan(buttons, 2); panel.Children.Add(buttons); Content = panel;
    }

    /// <summary>Gets the student entered by the user after successful confirmation.</summary>
    public StudentProfile? Student { get; private set; }

    private static void AddField(Grid panel, string label, TextBox textBox, int row)
    {
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(text, row); panel.Children.Add(text);
        textBox.Margin = new Thickness(0, 0, 0, 10); Grid.SetRow(textBox, row); Grid.SetColumn(textBox, 1); panel.Children.Add(textBox);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try { Student = new StudentProfile(studentIdTextBox.Text, nameTextBox.Text); DialogResult = true; }
        catch (ArgumentException exception) { MessageBox.Show(this, exception.Message, "请补全信息", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
