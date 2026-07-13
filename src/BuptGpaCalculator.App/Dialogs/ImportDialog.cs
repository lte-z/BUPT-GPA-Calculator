using System.Windows;
using System.Windows.Controls;
using BuptGpaCalculator.Core.Importing;

namespace BuptGpaCalculator.App.Dialogs;

/// <summary>Lets the user paste, preview, and confirm copied academic-system results.</summary>
public sealed class ImportDialog : Window
{
    private readonly TextBox pastedTextBox = new() { AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.NoWrap, MinHeight = 180 };
    private readonly TextBlock summaryTextBlock = new() { TextWrapping = TextWrapping.Wrap };
    private readonly DataGrid previewGrid = new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, MinHeight = 150 };
    private ImportParseResult? result;

    /// <summary>Initializes the import dialog.</summary>
    public ImportDialog()
    {
        Title = "导入教务成绩"; Width = 760; Height = 620; MinWidth = 640; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new Grid { Margin = new Thickness(18) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var instruction = new TextBlock { Text = "在教务系统成绩查询页面复制包含表头的完整结果表格，然后粘贴到下方。程序只读取剪贴板文本，不会登录或访问教务系统。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }; Grid.SetRow(instruction, 0); panel.Children.Add(instruction);
        Grid.SetRow(pastedTextBox, 1); panel.Children.Add(pastedTextBox);
        var previewButton = new Button { Content = "解析并预览", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 10) }; previewButton.Click += Preview_Click; Grid.SetRow(previewButton, 2); panel.Children.Add(previewButton);
        var previewPanel = new DockPanel(); DockPanel.SetDock(summaryTextBlock, Dock.Top); previewPanel.Children.Add(summaryTextBlock); previewPanel.Children.Add(previewGrid); Grid.SetRow(previewPanel, 3); panel.Children.Add(previewPanel);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; var cancel = new Button { Content = "取消", IsCancel = true }; var confirm = new Button { Content = "导入", IsDefault = true }; confirm.Click += Confirm_Click; buttons.Children.Add(cancel); buttons.Children.Add(confirm); Grid.SetRow(buttons, 4); panel.Children.Add(buttons); Content = panel;
    }

    /// <summary>Gets parsed valid courses after confirmation.</summary>
    public IReadOnlyList<ImportedCourse> Courses => result?.Courses ?? [];

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        result = AcademicScoreImportParser.Parse(pastedTextBox.Text);
        previewGrid.ItemsSource = result.Courses;
        summaryTextBlock.Text = result.Issues.Count == 0
            ? $"识别到 {result.Courses.Count} 门有效课程。"
            : $"识别到 {result.Courses.Count} 门有效课程；{result.Issues.Count} 行未导入：{string.Join("；", result.Issues.Take(3).Select(issue => issue.LineNumber == 0 ? issue.Message : $"第 {issue.LineNumber} 行：{issue.Message}"))}";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (result is null) Preview_Click(sender, e);
        if (result is null || result.Courses.Count == 0) { MessageBox.Show(this, "没有可导入的有效课程。", "无法导入", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        DialogResult = true;
    }
}
