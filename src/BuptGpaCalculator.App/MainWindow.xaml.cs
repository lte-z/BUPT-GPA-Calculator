using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using BuptGpaCalculator.App.Dialogs;
using BuptGpaCalculator.App.Models;
using BuptGpaCalculator.Core.Models;
using BuptGpaCalculator.Core.Persistence;
using BuptGpaCalculator.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace BuptGpaCalculator.App;

/// <summary>Hosts the archive, course editor, statistics, and calculation-rule views.</summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<CourseRow> courseRows = [];
    private readonly ICollectionView courseView;
    private ArchiveDatabase? archive;
    private StudentProfile? currentStudent;
    private bool isDirty;
    private bool isLoading;

    /// <summary>Initializes the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        courseView = CollectionViewSource.GetDefaultView(courseRows);
        courseView.Filter = FilterCourse;
        CourseDataGrid.ItemsSource = courseView;
        RuleDataGrid.ItemsSource = GpaScale.Rules;
        courseRows.CollectionChanged += RowsChanged;
        Loaded += (_, _) => Dispatcher.BeginInvoke(PromptForArchive);
    }

    private void PromptForArchive()
    {
        var choice = MessageBox.Show(this, "“是”新建成绩档案；“否”打开已有 .db 档案。", "BUPT GPA Calculator", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
        if (choice == MessageBoxResult.Yes) NewArchive_Click(this, new RoutedEventArgs());
        if (choice == MessageBoxResult.No) OpenArchive_Click(this, new RoutedEventArgs());
    }

    private async void NewArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSaveAsync()) return;
        var dialog = new SaveFileDialog { Title = "新建成绩档案", Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*", DefaultExt = ".db", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
            archive = await ArchiveDatabase.CreateAsync(dialog.FileName);
            courseRows.Clear(); currentStudent = null; isDirty = false;
            await LoadStudentsAsync(null);
            StatusTextBlock.Text = $"已新建档案：{archive.DatabasePath}";
            AddStudent_Click(this, new RoutedEventArgs());
        }
        catch (Exception exception) { ShowError("无法新建档案", exception); }
    }

    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSaveAsync()) return;
        var dialog = new OpenFileDialog { Title = "打开成绩档案", Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            archive = await ArchiveDatabase.OpenAsync(dialog.FileName);
            courseRows.Clear(); currentStudent = null; isDirty = false;
            await LoadStudentsAsync(null);
            StatusTextBlock.Text = $"已打开档案：{archive.DatabasePath}";
        }
        catch (Exception exception) { ShowError("无法打开档案", exception); }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync(true);

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (archive is null || !await SaveAsync(false)) return;
        var dialog = new SaveFileDialog { Title = "成绩档案另存为", Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*", DefaultExt = ".db", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.Copy(archive.DatabasePath, dialog.FileName, true);
            archive = await ArchiveDatabase.OpenAsync(dialog.FileName);
            StatusTextBlock.Text = $"已另存为：{archive.DatabasePath}";
        }
        catch (Exception exception) { ShowError("无法另存档案", exception); }
    }

    private async void AddStudent_Click(object sender, RoutedEventArgs e)
    {
        if (archive is null) { MessageBox.Show(this, "请先新建或打开一个成绩档案。", "尚未打开档案"); return; }
        var dialog = new StudentDialog { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Student is null) return;
        try
        {
            await archive.SaveStudentAsync(dialog.Student);
            await LoadStudentsAsync(dialog.Student.StudentId);
            StatusTextBlock.Text = $"已新增学生：{dialog.Student.Name}";
        }
        catch (Exception exception) { ShowError("无法保存学生信息", exception); }
    }

    private async void StudentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || archive is null || StudentComboBox.SelectedItem is not StudentItem item || item.Student.StudentId == currentStudent?.StudentId) return;
        if (!await ConfirmSaveAsync()) { isLoading = true; StudentComboBox.SelectedItem = StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault(x => x.Student.StudentId == currentStudent?.StudentId); isLoading = false; return; }
        await SelectStudentAsync(item.Student);
    }

    private void OverviewNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(OverviewPanel);
    private void CoursesNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(CoursesPanel);
    private void RulesNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(RulesPanel);

    private void AddCourse_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureStudent()) return;
        var row = new CourseRow(SuggestedTerm());
        courseRows.Add(row); CourseDataGrid.SelectedItem = row; CourseDataGrid.ScrollIntoView(row);
        isDirty = true; RefreshAll(); SetPage(CoursesPanel);
    }

    private void DeleteCourse_Click(object sender, RoutedEventArgs e)
    {
        var rows = CourseDataGrid.SelectedItems.OfType<CourseRow>().ToList();
        if (rows.Count == 0 || MessageBox.Show(this, $"确定删除所选 {rows.Count} 门课程吗？", "删除课程", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        foreach (var row in rows) courseRows.Remove(row);
        isDirty = true; RefreshAll();
    }

    private void ImportCourses_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureStudent()) return;
        var dialog = new ImportDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var added = 0; var updated = 0; var suspected = 0;
        foreach (var item in dialog.Courses)
        {
            var existing = string.IsNullOrWhiteSpace(item.CourseCode) ? null : courseRows.FirstOrDefault(row => row.Term.Trim() == item.Term.ToString() && row.CourseCode.Trim() == item.CourseCode);
            if (existing is not null)
            {
                existing.CourseName = item.CourseName; existing.ScoreText = item.Score.ToString(CultureInfo.InvariantCulture); existing.CreditText = item.Credit.ToString(CultureInfo.InvariantCulture); updated++; continue;
            }
            if (string.IsNullOrWhiteSpace(item.CourseCode) && courseRows.Any(row => row.Term.Trim() == item.Term.ToString() && row.CourseName.Trim() == item.CourseName)) suspected++;
            courseRows.Add(new CourseRow(item.Term.ToString()) { CourseCode = item.CourseCode ?? string.Empty, CourseName = item.CourseName, ScoreText = item.Score.ToString(CultureInfo.InvariantCulture), CreditText = item.Credit.ToString(CultureInfo.InvariantCulture), IsIncluded = true });
            added++;
        }
        if (added + updated > 0) isDirty = true;
        RefreshAll();
        MessageBox.Show(this, $"新增 {added} 门，更新 {updated} 门。" + (suspected > 0 ? $"\n{suspected} 门无课程编号的课程可能重复，已保留。" : string.Empty), "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenAcademicSystem_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://jwgl.bupt.edu.cn/") { UseShellExecute = true }); }
        catch (Exception exception) { ShowError("无法打开教务系统", exception); }
    }

    private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, "BUPT GPA Calculator\n\n面向北邮学生的本地成绩管理与 GPA 计算器。\n数据仅保存在你选择的 .db 档案中。\n\n小Z工作室#2026", "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    private void Filter_Changed(object sender, EventArgs e) => courseView.Refresh();
    private void OverviewScope_Changed(object sender, SelectionChangedEventArgs e) => RefreshStatistics();
    private void CourseDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(MarkDirty);
    private void CourseDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e) => Dispatcher.BeginInvoke(MarkDirty);
    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshCharts();

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!isDirty) return;
        var result = MessageBox.Show(this, "当前修改尚未保存。是否保存？", "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        if (result == MessageBoxResult.Yes) { e.Cancel = true; if (await SaveAsync(false)) { isDirty = false; Close(); } }
    }

    private async Task LoadStudentsAsync(string? selectId)
    {
        if (archive is null) return;
        var students = await archive.GetStudentsAsync();
        isLoading = true;
        StudentComboBox.ItemsSource = students.Select(student => new StudentItem(student)).ToList();
        var selected = StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault(item => item.Student.StudentId == selectId) ?? StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault();
        StudentComboBox.SelectedItem = selected;
        isLoading = false;
        if (selected is not null) await SelectStudentAsync(selected.Student); else RefreshAll();
    }

    private async Task SelectStudentAsync(StudentProfile student)
    {
        if (archive is null) return;
        isLoading = true; currentStudent = student; courseRows.Clear();
        foreach (var course in await archive.GetCoursesAsync(student.StudentId)) courseRows.Add(CourseRow.FromCourse(course));
        isDirty = false; isLoading = false; RefreshAll();
        StatusTextBlock.Text = $"当前学生：{student.Name}（{student.StudentId}）";
    }

    private async Task<bool> SaveAsync(bool showSuccess)
    {
        if (archive is null || currentStudent is null) { MessageBox.Show(this, "请先打开档案并选择一名学生。", "无法保存"); return false; }
        if (!TryBuildCourses(out var courses, out var error)) { MessageBox.Show(this, error, "请修正课程信息", MessageBoxButton.OK, MessageBoxImage.Warning); SetPage(CoursesPanel); return false; }
        try
        {
            await archive.SaveStudentAsync(currentStudent); await archive.ReplaceCoursesAsync(currentStudent.StudentId, courses);
            isDirty = false; RefreshAll(); StatusTextBlock.Text = $"已保存 {courses.Count} 门课程。";
            if (showSuccess) MessageBox.Show(this, "成绩档案已保存。", "保存完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (SqliteException exception) { MessageBox.Show(this, $"保存失败：同一学生、同一学期内的课程编号不能重复。\n\n{exception.Message}", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        catch (Exception exception) { ShowError("无法保存档案", exception); return false; }
    }

    private async Task<bool> ConfirmSaveAsync()
    {
        if (!isDirty) return true;
        var result = MessageBox.Show(this, "当前修改尚未保存。是否先保存？", "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes ? await SaveAsync(false) : result == MessageBoxResult.No;
    }

    private bool TryBuildCourses(out List<CourseRecord> courses, out string message)
    {
        courses = []; message = string.Empty;
        if (currentStudent is null) { message = "请先选择一名学生。"; return false; }
        for (var index = 0; index < courseRows.Count; index++)
        {
            if (!courseRows[index].TryToCourseRecord(currentStudent.StudentId, out var course, out var error)) { message = $"第 {index + 1} 行：{error}"; return false; }
            courses.Add(course!);
        }
        return true;
    }

    private bool FilterCourse(object item)
    {
        if (item is not CourseRow row) return false;
        var search = SearchTextBox?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(search) && !row.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase) && !row.CourseCode.Contains(search, StringComparison.OrdinalIgnoreCase) && !row.Term.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
        if (TermFilterComboBox?.SelectedItem is string term && term != "全部学期" && row.Term.Trim() != term) return false;
        return IncludedFilterComboBox?.SelectedItem is not ComboBoxItem filter || filter.Tag?.ToString() switch { "included" => row.IsIncluded, "excluded" => !row.IsIncluded, _ => true };
    }

    private void RowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null) foreach (var row in e.NewItems.OfType<CourseRow>()) row.PropertyChanged += (_, _) => MarkDirty();
    }

    private void MarkDirty()
    {
        if (isLoading) return;
        isDirty = true; RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshTermFilter(); RefreshOverviewScope(); courseView.Refresh(); RefreshStatistics();
    }

    private void RefreshTermFilter()
    {
        var previous = TermFilterComboBox.SelectedItem as string;
        var terms = courseRows.Select(row => row.Term.Trim()).Where(term => AcademicTerm.TryParse(term, out _)).Distinct().OrderByDescending(AcademicTerm.Parse).ToList();
        terms.Insert(0, "全部学期"); TermFilterComboBox.ItemsSource = terms;
        TermFilterComboBox.SelectedItem = terms.Contains(previous ?? string.Empty) ? previous : "全部学期";
    }

    private void RefreshOverviewScope()
    {
        var previous = OverviewTermComboBox.SelectedItem as string;
        var terms = courseRows.Select(row => row.Term.Trim()).Where(term => AcademicTerm.TryParse(term, out _)).Distinct().OrderByDescending(AcademicTerm.Parse).ToList();
        terms.Insert(0, "全部学期"); OverviewTermComboBox.ItemsSource = terms;
        OverviewTermComboBox.SelectedItem = terms.Contains(previous ?? string.Empty) ? previous : "全部学期";
    }

    private void RefreshStatistics()
    {
        if (!TryBuildCourses(out var courses, out _)) { GpaTextBlock.Text = GaTextBlock.Text = TotalCreditTextBlock.Text = CurrentTermGpaTextBlock.Text = "—"; CalculationDataGrid.ItemsSource = null; RefreshCharts(); return; }
        var scope = OverviewTermComboBox.SelectedItem as string;
        var scopedCourses = scope is null or "全部学期" ? courses : courses.Where(course => course.Term.ToString() == scope).ToList();
        var result = GpaCalculator.Calculate(scopedCourses);
        GpaTextBlock.Text = Format(result.Gpa, "0.000"); GaTextBlock.Text = Format(result.GradeAverage, "0.00"); TotalCreditTextBlock.Text = result.TotalCredit.ToString("0.##", CultureInfo.InvariantCulture);
        var latest = courses.Select(course => course.Term).OrderByDescending(term => term).FirstOrDefault();
        CurrentTermGpaTextBlock.Text = latest is null ? "—" : Format(GpaCalculator.Calculate(courses.Where(course => course.Term == latest)).Gpa, "0.000");
        OverviewSubtitleTextBlock.Text = currentStudent is null ? "打开档案后查看统计结果" : $"{currentStudent.Name} · 计入学分 {result.IncludedCredit:0.##}";
        CalculationDataGrid.ItemsSource = result.Contributions.Select(item => new CalculationRow(item)).ToList(); RefreshCharts(scopedCourses);
    }

    private void RefreshCharts(IReadOnlyList<CourseRecord>? records = null)
    {
        if (records is null) { if (!TryBuildCourses(out var built, out _)) { TrendCanvas.Children.Clear(); DistributionCanvas.Children.Clear(); return; } records = built; }
        DrawTrend(records); DrawDistribution(records);
    }

    private void DrawTrend(IReadOnlyList<CourseRecord> records)
    {
        TrendCanvas.Children.Clear(); var terms = records.Select(x => x.Term).Distinct().OrderBy(x => x).ToList();
        if (terms.Count == 0) { ChartMessage(TrendCanvas, "暂无可展示的课程记录"); return; }
        var width = Math.Max(TrendCanvas.ActualWidth, 220d); var height = Math.Max(TrendCanvas.ActualHeight, 160d); const double l = 28, r = 12, t = 16, b = 32;
        TrendCanvas.Children.Add(new Line { X1 = l, Y1 = t, X2 = l, Y2 = height - b, Stroke = Brushes.LightGray }); TrendCanvas.Children.Add(new Line { X1 = l, Y1 = height - b, X2 = width - r, Y2 = height - b, Stroke = Brushes.LightGray });
        var line = new Polyline { Stroke = (Brush)FindResource("AccentBrush"), StrokeThickness = 2.5 };
        for (var i = 0; i < terms.Count; i++) { var value = GpaCalculator.Calculate(records.Where(x => x.Term == terms[i])).Gpa ?? 0; var x = terms.Count == 1 ? (l + width - r) / 2 : l + (width - l - r) * i / (terms.Count - 1); var y = height - b - (double)(value / 4m) * (height - t - b); line.Points.Add(new Point(x, y)); var dot = new Ellipse { Width = 7, Height = 7, Fill = (Brush)FindResource("AccentBrush") }; Canvas.SetLeft(dot, x - 3.5); Canvas.SetTop(dot, y - 3.5); TrendCanvas.Children.Add(dot); Label(TrendCanvas, value.ToString("0.00", CultureInfo.InvariantCulture), x - 12, Math.Max(0, y - 21)); Label(TrendCanvas, terms[i].TermNumber.ToString(), x - 3, height - 23); }
        TrendCanvas.Children.Add(line); Label(TrendCanvas, "4.0", 2, t - 3); Label(TrendCanvas, "0", 12, height - b - 4);
    }

    private void DrawDistribution(IReadOnlyList<CourseRecord> records)
    {
        DistributionCanvas.Children.Clear(); if (records.Count == 0) { ChartMessage(DistributionCanvas, "暂无可展示的课程记录"); return; }
        var counts = new[] { records.Count(x => x.Score >= 90), records.Count(x => x.Score is >= 80 and < 90), records.Count(x => x.Score is >= 70 and < 80), records.Count(x => x.Score is >= 60 and < 70), records.Count(x => x.Score < 60) }; var labels = new[] { "90–100", "80–89", "70–79", "60–69", "<60" };
        var width = Math.Max(DistributionCanvas.ActualWidth, 220d); var height = Math.Max(DistributionCanvas.ActualHeight, 160d); var available = width - 32; var max = Math.Max(counts.Max(), 1);
        for (var i = 0; i < counts.Length; i++) { var slot = available / counts.Length; var barHeight = (height - 48) * counts[i] / max; var bar = new Rectangle { Width = Math.Min(34, slot - 10), Height = barHeight, Fill = (Brush)FindResource("AccentBrush"), RadiusX = 2, RadiusY = 2 }; var x = 20 + slot * i + (slot - bar.Width) / 2; Canvas.SetLeft(bar, x); Canvas.SetTop(bar, height - 32 - barHeight); DistributionCanvas.Children.Add(bar); Label(DistributionCanvas, counts[i].ToString(), x + bar.Width / 2 - 3, Math.Max(0, height - 51 - barHeight)); Label(DistributionCanvas, labels[i], x - 5, height - 23); }
    }

    private static void Label(Canvas canvas, string text, double left, double top) { var label = new TextBlock { Text = text, FontSize = 10, Foreground = Brushes.Gray }; Canvas.SetLeft(label, left); Canvas.SetTop(label, top); canvas.Children.Add(label); }
    private static void ChartMessage(Canvas canvas, string text) => Label(canvas, text, 16, 16);
    private void SetPage(UIElement target) { OverviewPanel.Visibility = target == OverviewPanel ? Visibility.Visible : Visibility.Collapsed; CoursesPanel.Visibility = target == CoursesPanel ? Visibility.Visible : Visibility.Collapsed; RulesPanel.Visibility = target == RulesPanel ? Visibility.Visible : Visibility.Collapsed; RefreshStatistics(); }
    private bool EnsureStudent() { if (currentStudent is not null) return true; MessageBox.Show(this, "请先选择或新增一名学生。", "尚未选择学生"); return false; }
    private string SuggestedTerm() { var latest = courseRows.Select(row => AcademicTerm.TryParse(row.Term, out var term) ? term : null).Where(x => x is not null).OrderByDescending(x => x).FirstOrDefault(); if (latest is not null) return latest.ToString(); var now = DateTime.Now; return new AcademicTerm(now.Month >= 8 ? now.Year : now.Year - 1, now.Month is >= 8 or <= 1 ? 1 : 2).ToString(); }
    private static string Format(decimal? value, string format) => value?.ToString(format, CultureInfo.InvariantCulture) ?? "—";
    private void ShowError(string title, Exception exception) => MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
