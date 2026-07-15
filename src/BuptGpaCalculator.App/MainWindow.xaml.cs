using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BuptGpaCalculator.App.Models;
using BuptGpaCalculator.Core.Importing;
using BuptGpaCalculator.Core.Models;
using BuptGpaCalculator.Core.Persistence;
using BuptGpaCalculator.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using DataGrid = System.Windows.Controls.DataGrid;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace BuptGpaCalculator.App;

/// <summary>Hosts one portable GPA archive with a compact fluent shell.</summary>
public partial class MainWindow
{
    private readonly ObservableCollection<CourseRow> courseRows = [];
    private readonly ListCollectionView courseView;
    private ArchiveDatabase? archive;
    private StudentProfile? currentStudent;
    private bool isDirty;
    private bool isLoading;
    private bool isClosingAfterConfirm;
    private bool isGradeAverageTrend = true;
    private string? sortProperty;
    private int sortPhase;

    /// <summary>Initializes the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AppVersionTextBlock.Text = $"BUPT GPA Calculator · v{GetDisplayVersion()}";
        courseView = (ListCollectionView)CollectionViewSource.GetDefaultView(courseRows);
        courseView.Filter = FilterCourse;
        CourseDataGrid.ItemsSource = courseView;
        RuleDataGrid.ItemsSource = GpaScale.Rules;
        courseRows.CollectionChanged += RowsChanged;
        UpdateTrendMetricText();
        SetPage(WelcomePanel);
        RefreshEmptyStates();
    }

    private async void WelcomePrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (archive is null)
        {
            NewArchive_Click(sender, e);
            return;
        }

        await AddStudentAsync();
    }

    private void WelcomeSecondaryButton_Click(object sender, RoutedEventArgs e) => OpenArchive_Click(sender, e);

    private async void NewArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSaveAsync()) return;

        var dialog = new SaveFileDialog
        {
            Title = "新建成绩档案",
            Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*",
            DefaultExt = ".db",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
            archive = await ArchiveDatabase.CreateAsync(dialog.FileName);
            courseRows.Clear();
            currentStudent = null;
            isDirty = false;
            await LoadStudentsAsync(null);
            SetStatus($"已新建档案：{archive.DatabasePath}");
            SetPage(OverviewPanel);
            await AddStudentAsync();
            if (currentStudent is null)
            {
                SetPage(WelcomePanel);
            }
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法新建档案", exception.Message);
        }
    }

    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmSaveAsync()) return;

        var dialog = new OpenFileDialog
        {
            Title = "打开成绩档案",
            Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            archive = await ArchiveDatabase.OpenAsync(dialog.FileName);
            courseRows.Clear();
            currentStudent = null;
            isDirty = false;
            await LoadStudentsAsync(null);
            SetStatus($"已打开档案：{archive.DatabasePath}");
            SetPage(currentStudent is null ? WelcomePanel : OverviewPanel);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法打开档案", exception.Message);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync(true);

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (archive is null)
        {
            await ShowInfoAsync("尚未打开档案", "请先新建或打开一个成绩档案。");
            return;
        }

        if (!await SaveAsync(false)) return;

        var dialog = new SaveFileDialog
        {
            Title = "成绩档案另存为",
            Filter = "成绩档案 (*.db)|*.db|所有文件 (*.*)|*.*",
            DefaultExt = ".db",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.Copy(archive.DatabasePath, dialog.FileName, true);
            archive = await ArchiveDatabase.OpenAsync(dialog.FileName);
            SetStatus($"已另存为：{archive.DatabasePath}");
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法另存档案", exception.Message);
        }
    }

    private async void AddStudent_Click(object sender, RoutedEventArgs e) => await AddStudentAsync();

    private async void ManageStudent_Click(object sender, RoutedEventArgs e) => await ManageCurrentStudentAsync();

    private async Task AddStudentAsync()
    {
        if (archive is null)
        {
            await ShowInfoAsync("尚未打开档案", "请先新建或打开一个成绩档案。");
            return;
        }

        if (!await ConfirmSaveAsync()) return;

        var student = await ShowStudentDialogAsync();
        if (student is null) return;

        try
        {
            await archive.SaveStudentAsync(student);
            await LoadStudentsAsync(student.StudentId);
            SetPage(OverviewPanel);
            SetStatus($"已新增学生：{student.Name}");
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法保存学生信息", exception.Message);
        }
    }

    private async Task ManageCurrentStudentAsync()
    {
        if (archive is null || currentStudent is null)
        {
            await ShowInfoAsync("尚未选择学生", "请先选择或新增一名学生。");
            return;
        }

        if (!await ConfirmSaveAsync()) return;

        var studentIdBlock = new TextBlock
        {
            Text = $"学号：{currentStudent.StudentId}",
            Opacity = 0.68,
            Margin = new Thickness(0, 0, 0, 10),
        };
        var nameBox = new TextBox { Text = currentStudent.Name, PlaceholderText = "姓名" };
        var hintBlock = new TextBlock
        {
            Text = "学号作为唯一标识不可修改。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.58,
            Margin = new Thickness(0, 10, 0, 0),
        };
        var panel = new StackPanel { Width = 380 };
        panel.Children.Add(studentIdBlock);
        panel.Children.Add(nameBox);
        panel.Children.Add(hintBlock);

        var result = await ShowContentDialogAsync("管理当前学生", panel, "保存姓名", "删除学生", "取消");
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var updated = new StudentProfile(currentStudent.StudentId, nameBox.Text);
                await archive.SaveStudentAsync(updated);
                currentStudent = updated;
                await LoadStudentsAsync(updated.StudentId);
                SetStatus($"已更新学生：{updated.Name}");
            }
            catch (ArgumentException exception)
            {
                await ShowErrorAsync("请修正学生信息", exception.Message);
            }
            catch (Exception exception)
            {
                await ShowErrorAsync("无法保存学生信息", exception.Message);
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            if (await ConfirmAsync("删除学生", $"确定删除“{currentStudent.Name}（{currentStudent.StudentId}）”及其全部课程吗？") != true) return;

            try
            {
                var deleted = currentStudent;
                await archive.DeleteStudentAsync(deleted.StudentId);
                currentStudent = null;
                courseRows.Clear();
                isDirty = false;
                await LoadStudentsAsync(null);
                SetPage(currentStudent is null ? WelcomePanel : OverviewPanel);
                SetStatus($"已删除学生：{deleted.Name}");
            }
            catch (Exception exception)
            {
                await ShowErrorAsync("无法删除学生", exception.Message);
            }
        }
    }

    private async void StudentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoading || archive is null || StudentComboBox.SelectedItem is not StudentItem item || item.Student.StudentId == currentStudent?.StudentId)
        {
            return;
        }

        if (!await ConfirmSaveAsync())
        {
            isLoading = true;
            StudentComboBox.SelectedItem = StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault(x => x.Student.StudentId == currentStudent?.StudentId);
            isLoading = false;
            return;
        }

        await SelectStudentAsync(item.Student);
    }

    private void OverviewNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(OverviewPanel);
    private void CoursesNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(CoursesPanel);
    private void RulesNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(RulesPanel);
    private void HelpNavigationButton_Click(object sender, RoutedEventArgs e) => SetPage(HelpPanel);

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dataGrid
            || FindVisualChild<ScrollViewer>(dataGrid) is not { } scrollViewer)
        {
            return;
        }

        var direction = e.Delta > 0 ? -1 : 1;
        var rowStep = GetDataGridRowStep(dataGrid);
        if (rowStep <= 0d)
        {
            rowStep = 32d;
        }

        var targetOffset = scrollViewer.VerticalOffset + direction * rowStep;
        targetOffset = Math.Clamp(targetOffset, 0d, scrollViewer.ScrollableHeight);
        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private async void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/lte-z/BUPT-GPA-Calculator") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法打开 GitHub 仓库", exception.Message);
        }
    }

    private async void OpenUserGuide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/lte-z/BUPT-GPA-Calculator/blob/main/docs/user-guide.md") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法打开用户指南", exception.Message);
        }
    }

    private async void AddCourse_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureStudentAsync()) return;

        var term = SuggestedTerm();
        var sortOrder = courseRows.Where(row => row.Term == term).Select(row => row.SortOrder).DefaultIfEmpty(-1).Max() + 1;
        var row = new CourseRow(term) { SortOrder = sortOrder };
        if (await ShowCourseDialogAsync(row, "新增课程") != true) return;

        courseRows.Add(row);
        CourseDataGrid.SelectedItem = row;
        CourseDataGrid.ScrollIntoView(row);
        isDirty = true;
        RefreshAll();
        SetStatus("已添加一门手动课程。");
    }

    private async void EditCourse_Click(object sender, RoutedEventArgs e) => await EditSelectedCourseAsync();

    private async void CourseDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) is null)
        {
            return;
        }

        await EditSelectedCourseAsync();
    }

    private void ReadOnlyDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid
            || FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        dataGrid.UnselectAll();
        dataGrid.UnselectAllCells();
        e.Handled = true;
    }

    private async Task EditSelectedCourseAsync()
    {
        var selectedRows = CourseDataGrid.SelectedItems.OfType<CourseRow>().ToList();
        if (selectedRows.Count == 0)
        {
            await ShowInfoAsync("尚未选择课程", "请先在课程表中选择一门课程。");
            return;
        }

        if (selectedRows.Count > 1)
        {
            await ShowInfoAsync("选择了多门课程", "编辑课程时请只选择一门课程。");
            return;
        }

        var selected = selectedRows[0];
        var draft = CloneRow(selected);
        if (await ShowCourseDialogAsync(draft, "编辑课程") != true) return;

        draft.Source = selected.Source == CourseSource.Manual ? CourseSource.Manual : CourseSource.Modified;
        CopyRow(draft, selected);
        isDirty = true;
        RefreshAll();
        SetStatus($"已更新课程：{selected.CourseName}");
    }

    private async void DeleteCourse_Click(object sender, RoutedEventArgs e)
    {
        var rows = CourseDataGrid.SelectedItems.OfType<CourseRow>().ToList();
        if (rows.Count == 0)
        {
            await ShowInfoAsync("尚未选择课程", "请先在课程表中选择要删除的课程。");
            return;
        }

        if (await ConfirmAsync("删除课程", $"确定删除所选 {rows.Count} 门课程吗？") != true) return;

        foreach (var row in rows)
        {
            courseRows.Remove(row);
        }

        isDirty = true;
        RefreshAll();
        SetStatus($"已删除 {rows.Count} 门课程。");
    }

    private async void ImportCourses_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureStudentAsync()) return;

        if (!TryBuildCourses(out var currentCourses, out var error))
        {
            await ShowErrorAsync("无法导入", $"导入前请先修正现有课程信息。\n\n{error}");
            SetPage(CoursesPanel);
            return;
        }

        var importedCourses = await ShowImportDialogAsync();
        if (importedCourses is null) return;

        try
        {
            var merge = CourseImportMerger.Merge(currentStudent!.StudentId, currentCourses, importedCourses);
            ReplaceRows(merge.Courses);
            isDirty = true;
            RefreshAll();
            SetStatus($"导入完成：新增 {merge.AddedCount} 门，更新 {merge.UpdatedCount} 门。");
        }
        catch (ArgumentException exception)
        {
            await ShowErrorAsync("无法导入", exception.Message);
        }
    }

    private void Filter_Changed(object sender, EventArgs e)
    {
        if (sender is FrameworkElement { IsLoaded: true })
        {
            courseView.Refresh();
            RefreshEmptyStates();
        }
    }

    private void OverviewScope_Changed(object sender, SelectionChangedEventArgs e) => RefreshStatistics();
    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshCharts();

    private void ToggleTrendMetric_Click(object sender, RoutedEventArgs e)
    {
        isGradeAverageTrend = !isGradeAverageTrend;
        UpdateTrendMetricText();
        RefreshCharts();
    }

    private void CourseDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var property = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(property)) return;

        sortPhase = property == sortProperty ? (sortPhase + 1) % 3 : 0;
        sortProperty = property;
        foreach (var column in CourseDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        if (sortPhase == 2)
        {
            courseView.CustomSort = null;
            sortProperty = null;
            courseView.Refresh();
            return;
        }

        var direction = sortPhase == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending;
        e.Column.SortDirection = direction;
        courseView.CustomSort = new CourseRowComparer(property, direction);
        courseView.Refresh();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (isClosingAfterConfirm || !isDirty) return;

        e.Cancel = true;
        if (!await ConfirmSaveAsync()) return;

        isClosingAfterConfirm = true;
        Close();
    }

    private async Task LoadStudentsAsync(string? selectId)
    {
        if (archive is null) return;

        var students = await archive.GetStudentsAsync();
        isLoading = true;
        StudentComboBox.ItemsSource = students.Select(student => new StudentItem(student)).ToList();
        var selected = StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault(item => item.Student.StudentId == selectId)
            ?? StudentComboBox.Items.OfType<StudentItem>().FirstOrDefault();
        StudentComboBox.SelectedItem = selected;
        isLoading = false;

        if (selected is not null)
        {
            await SelectStudentAsync(selected.Student);
        }
        else
        {
            RefreshAll();
        }
    }

    private async Task SelectStudentAsync(StudentProfile student)
    {
        if (archive is null) return;

        isLoading = true;
        currentStudent = student;
        ReplaceRows(await archive.GetCoursesAsync(student.StudentId));
        isDirty = false;
        isLoading = false;
        RefreshAll();
        SetStatus($"当前学生：{student.Name}（{student.StudentId}）");
    }

    private async Task<bool> SaveAsync(bool showSuccess)
    {
        if (archive is null || currentStudent is null)
        {
            await ShowInfoAsync("无法保存", "请先打开档案并选择一名学生。");
            return false;
        }

        if (!TryBuildCourses(out var courses, out var error))
        {
            await ShowErrorAsync("请修正课程信息", error);
            SetPage(CoursesPanel);
            return false;
        }

        try
        {
            await archive.SaveStudentAsync(currentStudent);
            await archive.ReplaceCoursesAsync(currentStudent.StudentId, courses);
            isDirty = false;
            RefreshStatistics();
            SetStatus($"已保存 {courses.Count} 门课程。");
            if (showSuccess)
            {
                ShowToast("保存完成", "成绩档案已写入当前 .db 文件。");
            }

            return true;
        }
        catch (SqliteException exception)
        {
            await ShowErrorAsync("无法保存", $"同一学生、同一学期内的课程编号不能重复。\n\n{exception.Message}");
            return false;
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法保存档案", exception.Message);
            return false;
        }
    }

    private async Task<bool> ConfirmSaveAsync()
    {
        if (!isDirty) return true;

        var result = await AskAsync("未保存的修改", "当前修改尚未保存。是否先保存？", "保存", "不保存", "取消");
        return result switch
        {
            ContentDialogResult.Primary => await SaveAsync(false),
            ContentDialogResult.Secondary => true,
            _ => false,
        };
    }

    /// <summary>Reports a UI-thread exception without terminating the app.</summary>
    /// <param name="exception">The unexpected exception.</param>
    public async void ReportUnexpectedException(Exception exception)
    {
        SetStatus($"发生错误：{exception.Message}");
        await ShowErrorAsync("程序遇到问题", exception.Message);
    }

    private bool TryBuildCourses(out List<CourseRecord> courses, out string message)
    {
        courses = [];
        message = string.Empty;

        if (currentStudent is null)
        {
            message = "请先选择一名学生。";
            return false;
        }

        for (var index = 0; index < courseRows.Count; index++)
        {
            if (!courseRows[index].TryToCourseRecord(currentStudent.StudentId, out var course, out var error))
            {
                message = $"第 {index + 1} 行：{error}";
                return false;
            }

            courses.Add(course!);
        }

        return true;
    }

    private IReadOnlyList<CourseRecord> GetValidCourses()
    {
        if (currentStudent is null) return [];
        return courseRows
            .Select(row => row.TryToCourseRecord(currentStudent.StudentId, out var course, out _) ? course : null)
            .OfType<CourseRecord>()
            .ToList();
    }

    private bool FilterCourse(object item)
    {
        if (item is not CourseRow row) return false;

        var search = SearchTextBox?.Text.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(search)
            && !row.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase)
            && !row.CourseCode.Contains(search, StringComparison.OrdinalIgnoreCase)
            && !row.Term.Contains(search, StringComparison.OrdinalIgnoreCase)
            && !row.ScoreDisplayText.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TermFilterComboBox?.SelectedItem is string term && term != "全部学期" && row.Term != term)
        {
            return false;
        }

        return IncludedFilterComboBox?.SelectedItem is not ComboBoxItem filter
            || filter.Tag?.ToString() switch
            {
                "included" => row.IsIncluded,
                "excluded" => !row.IsIncluded,
                _ => true,
            };
    }

    private void RowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var row in e.NewItems.OfType<CourseRow>())
            {
                row.PropertyChanged += Row_PropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var row in e.OldItems.OfType<CourseRow>())
            {
                row.PropertyChanged -= Row_PropertyChanged;
            }
        }
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!isLoading) isDirty = true;
    }

    private void RefreshAll()
    {
        RefreshTermFilter();
        RefreshOverviewScope();
        courseView.Refresh();
        RefreshStatistics();
        RefreshEmptyStates();
    }

    private void RefreshTermFilter()
    {
        var previous = TermFilterComboBox.SelectedItem as string;
        var terms = courseRows
            .Select(row => row.Term)
            .Where(term => AcademicTerm.TryParse(term, out _))
            .Distinct()
            .OrderByDescending(AcademicTerm.Parse)
            .ToList();
        terms.Insert(0, "全部学期");
        TermFilterComboBox.ItemsSource = terms;
        TermFilterComboBox.SelectedItem = terms.Contains(previous ?? string.Empty) ? previous : "全部学期";
    }

    private void RefreshOverviewScope()
    {
        var previousKey = (OverviewTermComboBox.SelectedItem as OverviewScopeOption)?.Key;
        var terms = courseRows
            .Select(row => row.Term)
            .Where(term => AcademicTerm.TryParse(term, out _))
            .Select(AcademicTerm.Parse)
            .Distinct()
            .OrderByDescending(term => term)
            .ToList();

        var options = new List<OverviewScopeOption> { OverviewScopeOption.All };
        options.AddRange(terms
            .Select(term => term.StartYear)
            .Distinct()
            .OrderByDescending(year => year)
            .Select(year => OverviewScopeOption.ForAcademicYear(year)));
        options.AddRange(terms.Select(OverviewScopeOption.ForTerm));

        OverviewTermComboBox.ItemsSource = options;
        OverviewTermComboBox.SelectedItem = options.FirstOrDefault(option => option.Key == previousKey) ?? OverviewScopeOption.All;
    }

    private void RefreshStatistics()
    {
        var courses = GetValidCourses();
        if (courses.Count == 0)
        {
            GpaTextBlock.Text = GaTextBlock.Text = TotalCreditTextBlock.Text = CurrentTermGaTextBlock.Text = "—";
            CurrentTermGaCard.Opacity = 1;
            OverviewSubtitleTextBlock.Text = currentStudent is null ? "打开档案后查看统计结果" : $"{currentStudent.Name} · 请前往“课程记录”导入或新增课程";
            CalculationDataGrid.ItemsSource = null;
            RefreshCharts([]);
            RefreshEmptyStates();
            return;
        }

        var scope = OverviewTermComboBox.SelectedItem as OverviewScopeOption ?? OverviewScopeOption.All;
        var scopedCourses = scope.Filter(courses).ToList();
        var result = GpaCalculator.Calculate(scopedCourses);
        GpaTextBlock.Text = Format(result.Gpa);
        GaTextBlock.Text = Format(result.GradeAverage);
        TotalCreditTextBlock.Text = result.TotalCredit.ToString("0.0", CultureInfo.InvariantCulture);
        var latest = courses.Select(course => course.Term).OrderByDescending(term => term).First();
        CurrentTermGaTextBlock.Text = Format(GpaCalculator.Calculate(courses.Where(course => course.Term == latest)).GradeAverage);
        CurrentTermGaCard.Opacity = scope.IsAll ? 1 : 0.42;
        OverviewSubtitleTextBlock.Text = currentStudent is null ? "打开档案后查看统计结果" : $"{currentStudent.Name} · {scope.DisplayName} · 计入学分 {result.IncludedCredit:0.0}";
        CalculationDataGrid.ItemsSource = result.Contributions.Select(item => new CalculationRow(item)).ToList();
        RefreshCharts(scopedCourses);
        RefreshEmptyStates();
    }

    private void RefreshEmptyStates()
    {
        EmptyCoursesHintPanel.Visibility = courseRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyCalculationHintPanel.Visibility = CalculationDataGrid.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshCharts(IReadOnlyList<CourseRecord>? records = null) => DrawCharts(records ?? GetCurrentScopedCourses());

    private IReadOnlyList<CourseRecord> GetCurrentScopedCourses()
    {
        var courses = GetValidCourses();
        var scope = OverviewTermComboBox.SelectedItem as OverviewScopeOption ?? OverviewScopeOption.All;
        return scope.Filter(courses).ToList();
    }

    private void DrawCharts(IReadOnlyList<CourseRecord> records)
    {
        DrawTrend(records);
        DrawDistribution(records);
    }

    private void DrawTrend(IReadOnlyList<CourseRecord> records)
    {
        UpdateTrendMetricText();
        TrendCanvas.Children.Clear();
        var terms = records.Select(x => x.Term).Distinct().OrderBy(x => x).ToList();
        if (terms.Count == 0)
        {
            ChartMessage(TrendCanvas, isGradeAverageTrend ? "添加课程后显示 GA 趋势" : "添加课程后显示 GPA 趋势");
            return;
        }

        var width = Math.Max(TrendCanvas.ActualWidth, 220d);
        var height = Math.Max(TrendCanvas.ActualHeight, 160d);
        const double left = 46, right = 28, top = 16, bottom = 32;
        const double plotInset = 22;
        var maximum = isGradeAverageTrend ? 100m : 4m;
        var topLabel = isGradeAverageTrend ? "100" : "4.0";
        var plotLeft = left + plotInset;
        var plotRight = width - right - plotInset;
        TrendCanvas.Children.Add(new Line { X1 = left, Y1 = top, X2 = left, Y2 = height - bottom, Stroke = Brushes.Gray, Opacity = .35 });
        TrendCanvas.Children.Add(new Line { X1 = left, Y1 = height - bottom, X2 = width - right, Y2 = height - bottom, Stroke = Brushes.Gray, Opacity = .35 });

        var line = new Polyline { Stroke = TryFindResource("AccentTextFillColorPrimaryBrush") as Brush ?? Brushes.DodgerBlue, StrokeThickness = 2.5 };
        for (var i = 0; i < terms.Count; i++)
        {
            var result = GpaCalculator.Calculate(records.Where(x => x.Term == terms[i]));
            var value = isGradeAverageTrend ? result.GradeAverage ?? 0 : result.Gpa ?? 0;
            var x = terms.Count == 1 ? (plotLeft + plotRight) / 2 : plotLeft + (plotRight - plotLeft) * i / (terms.Count - 1);
            var y = height - bottom - (double)(value / maximum) * (height - top - bottom);
            line.Points.Add(new Point(x, y));

            var dot = new Ellipse { Width = 7, Height = 7, Fill = line.Stroke };
            Canvas.SetLeft(dot, x - 3.5);
            Canvas.SetTop(dot, y - 3.5);
            TrendCanvas.Children.Add(dot);
            Label(TrendCanvas, value.ToString("0.0000", CultureInfo.InvariantCulture), x - 18, Math.Max(0, y - 21));
            Label(TrendCanvas, ShortTermLabel(terms[i]), x - 20, height - 23);
        }

        TrendCanvas.Children.Add(line);
        Label(TrendCanvas, topLabel, 2, top - 3);
        Label(TrendCanvas, "0", 14, height - bottom - 4);
    }

    private void UpdateTrendMetricText()
    {
        TrendTitleTextBlock.Text = isGradeAverageTrend ? "学期 GA 趋势" : "学期 GPA 趋势";
        ToggleTrendMetricButton.ToolTip = isGradeAverageTrend ? "切换为 GPA 趋势" : "切换为 GA 趋势";
    }

    private static string ShortTermLabel(AcademicTerm term)
        => $"{term.StartYear % 100:00}-{(term.StartYear + 1) % 100:00}-{term.TermNumber}";

    private void DrawDistribution(IReadOnlyList<CourseRecord> records)
    {
        DistributionCanvas.Children.Clear();
        if (records.Count == 0)
        {
            ChartMessage(DistributionCanvas, "添加课程后显示成绩分布");
            return;
        }

        var counts = new[]
        {
            records.Count(x => x.Score >= 90),
            records.Count(x => x.Score is >= 80 and < 90),
            records.Count(x => x.Score is >= 70 and < 80),
            records.Count(x => x.Score is >= 60 and < 70),
            records.Count(x => x.Score < 60),
        };
        var labels = new[] { "90-100", "80-89", "70-79", "60-69", "<60" };
        var fill = TryFindResource("AccentTextFillColorPrimaryBrush") as Brush ?? Brushes.DodgerBlue;
        var width = Math.Max(DistributionCanvas.ActualWidth, 220d);
        var height = Math.Max(DistributionCanvas.ActualHeight, 160d);
        var available = width - 32;
        var max = Math.Max(counts.Max(), 1);

        for (var i = 0; i < counts.Length; i++)
        {
            var slot = available / counts.Length;
            var barHeight = (height - 48) * counts[i] / max;
            var bar = new Rectangle { Width = Math.Min(34, slot - 10), Height = barHeight, Fill = fill, RadiusX = 3, RadiusY = 3 };
            var x = 20 + slot * i + (slot - bar.Width) / 2;
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, height - 32 - barHeight);
            DistributionCanvas.Children.Add(bar);
            Label(DistributionCanvas, counts[i].ToString(CultureInfo.InvariantCulture), x + bar.Width / 2 - 3, Math.Max(0, height - 51 - barHeight));
            Label(DistributionCanvas, labels[i], x - 5, height - 23);
        }
    }

    private void ReplaceRows(IEnumerable<CourseRecord> records)
    {
        isLoading = true;
        courseRows.Clear();
        foreach (var record in records.OrderBy(course => course.Term).ThenBy(course => course.SortOrder))
        {
            courseRows.Add(CourseRow.FromCourse(record));
        }

        isLoading = false;
    }

    private static CourseRow CloneRow(CourseRow source)
    {
        var clone = new CourseRow(source.Term)
        {
            CourseCode = source.CourseCode,
            CourseName = source.CourseName,
            ScoreText = source.ScoreText,
            CreditText = source.CreditText,
            IsIncluded = source.IsIncluded,
            Source = source.Source,
            SortOrder = source.SortOrder,
        };
        return clone;
    }

    private static void CopyRow(CourseRow source, CourseRow target)
    {
        target.Term = source.Term;
        target.CourseCode = source.CourseCode;
        target.CourseName = source.CourseName;
        target.ScoreText = source.ScoreText;
        target.CreditText = source.CreditText;
        target.IsIncluded = source.IsIncluded;
        target.Source = source.Source;
        target.SortOrder = source.SortOrder;
    }

    private async Task<bool> EnsureStudentAsync()
    {
        if (currentStudent is not null) return true;
        await ShowInfoAsync("尚未选择学生", "请先选择或新增一名学生。");
        return false;
    }

    private string SuggestedTerm()
    {
        var latest = courseRows
            .Select(row => AcademicTerm.TryParse(row.Term, out var term) ? term : null)
            .Where(x => x is not null)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        if (latest is not null) return latest.ToString();

        var now = DateTime.Now;
        return new AcademicTerm(now.Month >= 8 ? now.Year : now.Year - 1, now.Month is >= 8 or <= 1 ? 1 : 2).ToString();
    }

    private void SetPage(UIElement target)
    {
        UpdateWelcomePanel();
        WelcomePanel.Visibility = target == WelcomePanel ? Visibility.Visible : Visibility.Collapsed;
        OverviewPanel.Visibility = target == OverviewPanel ? Visibility.Visible : Visibility.Collapsed;
        CoursesPanel.Visibility = target == CoursesPanel ? Visibility.Visible : Visibility.Collapsed;
        RulesPanel.Visibility = target == RulesPanel ? Visibility.Visible : Visibility.Collapsed;
        HelpPanel.Visibility = target == HelpPanel ? Visibility.Visible : Visibility.Collapsed;

        SetNavigationAppearance(OverviewNavButton, target == OverviewPanel);
        SetNavigationAppearance(CoursesNavButton, target == CoursesPanel);
        SetNavigationAppearance(RulesNavButton, target == RulesPanel);
        SetNavigationAppearance(HelpNavButton, target == HelpPanel);
        RefreshStatistics();
    }

    private void UpdateWelcomePanel()
    {
        if (archive is null)
        {
            WelcomeTitleTextBlock.Text = "从一个成绩档案开始";
            WelcomePrimaryTextBlock.Text = "档案是独立的单个 .db 文件";
            WelcomeSecondaryTextBlock.Text = "程序不会在其他位置写入成绩数据";
            WelcomePrimaryButton.Content = "新建成绩档案";
            WelcomeSecondaryButton.Content = "打开已有档案";
            return;
        }

        WelcomeTitleTextBlock.Text = "添加第一个学生";
        WelcomePrimaryTextBlock.Text = "当前档案还没有学生";
        WelcomeSecondaryTextBlock.Text = "添加学生后即可录入或导入课程";
        WelcomePrimaryButton.Content = "新增学生";
        WelcomeSecondaryButton.Content = "打开其他档案";
    }

    private static void SetNavigationAppearance(Button button, bool selected) => button.Appearance = selected ? ControlAppearance.Primary : ControlAppearance.Secondary;

    private async Task<StudentProfile?> ShowStudentDialogAsync()
    {
        while (true)
        {
            var idBox = new TextBox { PlaceholderText = "学号" };
            var nameBox = new TextBox { PlaceholderText = "姓名", Margin = new Thickness(0, 10, 0, 0) };
            var panel = new StackPanel { Width = 340 };
            panel.Children.Add(idBox);
            panel.Children.Add(nameBox);

            var result = await ShowContentDialogAsync("新增学生", panel, "保存", null, "取消");
            if (result != ContentDialogResult.Primary) return null;

            try
            {
                return new StudentProfile(idBox.Text, nameBox.Text);
            }
            catch (ArgumentException exception)
            {
                await ShowErrorAsync("请补全信息", exception.Message);
            }
        }
    }

    private async Task<bool?> ShowCourseDialogAsync(CourseRow row, string title)
    {
        while (true)
        {
            var term = AcademicTerm.TryParse(row.Term, out var parsedTerm)
                ? parsedTerm
                : AcademicTerm.Parse(SuggestedTerm());
            var startYearBox = new TextBox
            {
                Text = term.StartYear.ToString(CultureInfo.InvariantCulture),
                Width = 90,
                PlaceholderText = "起始年",
            };
            var endYearBlock = new TextBlock
            {
                Text = (term.StartYear + 1).ToString(CultureInfo.InvariantCulture),
                Width = 42,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Opacity = 0.72,
            };
            var termNumberBox = new ComboBox
            {
                Width = 64,
                ItemsSource = new[] { 1, 2, 3 },
                SelectedItem = term.TermNumber,
            };
            startYearBox.TextChanged += (_, _) =>
            {
                if (int.TryParse(startYearBox.Text.Trim(), out var startYear))
                {
                    endYearBlock.Text = (startYear + 1).ToString(CultureInfo.InvariantCulture);
                }
            };
            var termPanel = new StackPanel();
            termPanel.Children.Add(new TextBlock { Text = "开课学期", Opacity = 0.68, Margin = new Thickness(0, 0, 0, 5) });
            var termInputsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            termInputsPanel.Children.Add(startYearBox);
            termInputsPanel.Children.Add(new TextBlock { Text = "-", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0), Opacity = 0.58 });
            termInputsPanel.Children.Add(endYearBlock);
            termInputsPanel.Children.Add(new TextBlock { Text = "-", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0), Opacity = 0.58 });
            termInputsPanel.Children.Add(termNumberBox);
            termPanel.Children.Add(termInputsPanel);
            var codeBox = new TextBox { Text = row.CourseCode, PlaceholderText = "课程编号（可选）", Margin = new Thickness(0, 8, 0, 0) };
            var nameBox = new TextBox { Text = row.CourseName, PlaceholderText = "课程名称", Margin = new Thickness(0, 8, 0, 0) };
            var scoreBox = new TextBox
            {
                Text = row.ScoreText,
                PlaceholderText = "成绩（如 89.5、优、通过）",
                Margin = new Thickness(0, 8, 0, 0),
            };
            var creditBox = new TextBox { Text = row.CreditText, PlaceholderText = "学分（可为 0）", Margin = new Thickness(0, 8, 0, 0) };
            var includedBox = new CheckBox { Content = "计入 GPA 与 GA", IsChecked = row.IsIncluded, Margin = new Thickness(0, 12, 0, 0) };
            var panel = new StackPanel { Width = 420 };
            panel.Children.Add(termPanel);
            panel.Children.Add(codeBox);
            panel.Children.Add(nameBox);
            panel.Children.Add(scoreBox);
            panel.Children.Add(creditBox);
            panel.Children.Add(includedBox);

            var result = await ShowContentDialogAsync(title, panel, "保存", null, "取消");
            if (result != ContentDialogResult.Primary) return false;

            if (!int.TryParse(startYearBox.Text.Trim(), out var startYear)
                || termNumberBox.SelectedItem is not int termNumber)
            {
                await ShowErrorAsync("请修正课程信息", "开课学期必须包含有效的起始年份和学期编号。");
                continue;
            }

            try
            {
                row.Term = new AcademicTerm(startYear, termNumber).ToString();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                await ShowErrorAsync("请修正课程信息", exception.Message);
                continue;
            }

            row.CourseCode = codeBox.Text;
            row.CourseName = nameBox.Text;
            row.ScoreText = scoreBox.Text;
            row.CreditText = creditBox.Text;
            row.IsIncluded = includedBox.IsChecked == true;

            if (currentStudent is not null && !row.TryToCourseRecord(currentStudent.StudentId, out _, out var error))
            {
                await ShowErrorAsync("请修正课程信息", error ?? "课程信息无效。");
                continue;
            }

            return true;
        }
    }

    private async Task<IReadOnlyList<ImportedCourse>?> ShowImportDialogAsync()
    {
        while (true)
        {
            ImportParseResult? parseResult = null;
            var inputBox = new System.Windows.Controls.TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                Height = 132,
            };
            var instructionBlock = new TextBlock
            {
                Text = "在成绩表格内点一下，按 Ctrl+A、Ctrl+C；回到这里点“识别成绩表”。也可以手动粘贴后再识别。",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 6),
            };
            var summaryBlock = new TextBlock
            {
                Text = "等待识别成绩表。",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var previewGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                Focusable = false,
                IsReadOnly = true,
                Height = 164,
            };
            previewGrid.SetValue(VirtualizingPanel.ScrollUnitProperty, ScrollUnit.Pixel);
            previewGrid.PreviewMouseWheel += DataGrid_PreviewMouseWheel;
            previewGrid.PreviewMouseLeftButtonDown += ReadOnlyDataGrid_PreviewMouseLeftButtonDown;
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "原文行", Binding = new Binding(nameof(ImportedCourse.SourceLineNumber)), Width = 64 });
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "学期", Binding = new Binding(nameof(ImportedCourse.Term)), Width = 108 });
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "课程编号", Binding = new Binding(nameof(ImportedCourse.CourseCode)) { TargetNullValue = string.Empty }, Width = 112 });
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "课程名称", Binding = new Binding(nameof(ImportedCourse.CourseName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "成绩", Binding = new Binding(nameof(ImportedCourse.ScoreDisplayText)), Width = 76 });
            previewGrid.Columns.Add(new DataGridTextColumn { Header = "学分", Binding = new Binding(nameof(ImportedCourse.Credit)) { StringFormat = "0.0" }, Width = 62 });
            var panel = new Grid
            {
                Width = 740,
            };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(instructionBlock, 0);
            panel.Children.Add(instructionBlock);
            Grid.SetRow(inputBox, 1);
            panel.Children.Add(inputBox);

            var actions = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var openButton = new Button { Content = "打开教务系统", Appearance = ControlAppearance.Secondary, Margin = new Thickness(0, 0, 6, 0) };
            var previewButton = new Button { Content = "识别成绩表", Appearance = ControlAppearance.Primary };
            buttons.Children.Add(openButton);
            buttons.Children.Add(previewButton);
            Grid.SetColumn(buttons, 0);
            actions.Children.Add(buttons);
            Grid.SetColumn(summaryBlock, 1);
            actions.Children.Add(summaryBlock);
            Grid.SetRow(actions, 2);
            panel.Children.Add(actions);

            Grid.SetRow(previewGrid, 3);
            panel.Children.Add(previewGrid);

            void Parse()
            {
                parseResult = AcademicScoreImportParser.Parse(inputBox.Text);
                previewGrid.ItemsSource = parseResult.Courses;
                summaryBlock.Text = parseResult.Issues.Count == 0
                    ? $"识别到 {parseResult.Courses.Count} 门有效课程。"
                    : $"识别到 {parseResult.Courses.Count} 门有效课程；{parseResult.Issues.Count} 行未导入：{string.Join("；", parseResult.Issues.Take(3).Select(issue => issue.LineNumber == 0 ? issue.Message : $"第 {issue.LineNumber} 行：{issue.Message}"))}";
            }

            async Task RecognizeAsync()
            {
                if (string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    try
                    {
                        if (!Clipboard.ContainsText())
                        {
                            summaryBlock.Text = "剪贴板中没有可读取的文本。请先复制成绩表，或手动粘贴后再识别。";
                            return;
                        }

                        inputBox.Text = Clipboard.GetText();
                    }
                    catch (Exception exception)
                    {
                        await ShowErrorAsync("无法读取剪贴板", exception.Message);
                        return;
                    }
                }

                Parse();
            }

            openButton.Click += async (_, _) => await OpenAcademicSystemAsync();
            previewButton.Click += async (_, _) => await RecognizeAsync();

            var result = await ShowContentDialogAsync("导入教务成绩", panel, "导入", null, "取消");
            if (result != ContentDialogResult.Primary) return null;

            parseResult ??= AcademicScoreImportParser.Parse(inputBox.Text);
            if (parseResult.Courses.Count == 0)
            {
                await ShowErrorAsync("无法导入", "没有识别到可导入的有效课程。");
                continue;
            }

            return parseResult.Courses;
        }
    }

    private async Task OpenAcademicSystemAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://jwgl.bupt.edu.cn/") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("无法打开教务系统", exception.Message);
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
        => await AskAsync(title, message, "确定", null, "取消") == ContentDialogResult.Primary;

    private async Task ShowInfoAsync(string title, string message)
        => await ShowContentDialogAsync(title, Text(message), null, null, "知道了");

    private async Task ShowErrorAsync(string title, string message)
        => await ShowContentDialogAsync(title, Text(message), null, null, "知道了");

    private async Task<ContentDialogResult> AskAsync(string title, string message, string primary, string? secondary, string close)
        => await ShowContentDialogAsync(title, Text(message), primary, secondary, close);

    private async Task<ContentDialogResult> ShowContentDialogAsync(string title, object content, string? primary, string? secondary, string close)
    {
        var dialog = new ContentDialog(RootDialogHost)
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primary ?? string.Empty,
            SecondaryButtonText = secondary ?? string.Empty,
            CloseButtonText = close,
        };

        return await dialog.ShowAsync();
    }

    private static TextBlock Text(string message) => new() { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };

    private void ShowToast(string title, string message)
    {
        try
        {
            var snackbar = new Snackbar(RootSnackbarPresenter)
            {
                Title = title,
                Content = message,
                Timeout = TimeSpan.FromSeconds(2.5),
            };
            snackbar.Show();
        }
        catch
        {
            SetStatus($"{title}：{message}");
        }
    }

    private void SetStatus(string message) => StatusTextBlock.Text = message;

    private static string GetDisplayVersion()
    {
        var version = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
        }

        return (version ?? "0.1.1").Split('+')[0];
    }

    private static string Format(decimal? value) => value?.ToString("0.0000", CultureInfo.InvariantCulture) ?? "—";

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(source); i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static double GetDataGridRowStep(DataGrid dataGrid)
    {
        var rows = FindVisualChildren<DataGridRow>(dataGrid).Take(2).ToList();
        if (rows.Count >= 2)
        {
            var firstTop = rows[0].TransformToAncestor(dataGrid).Transform(new Point(0, 0)).Y;
            var secondTop = rows[1].TransformToAncestor(dataGrid).Transform(new Point(0, 0)).Y;
            var visualStep = Math.Abs(secondTop - firstTop);
            if (visualStep > 0d)
            {
                return visualStep;
            }
        }

        return rows.FirstOrDefault()?.ActualHeight ?? 0d;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(source); i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Label(Canvas canvas, string text, double left, double top)
    {
        var label = new TextBlock { Text = text, FontSize = 10, Foreground = Brushes.Gray };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        canvas.Children.Add(label);
    }

    private static void ChartMessage(Canvas canvas, string text) => Label(canvas, text, 16, 16);

    private sealed class CourseRowComparer(string property, ListSortDirection direction) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is not CourseRow left || y is not CourseRow right) return 0;

            var result = property switch
            {
                "Term" => CompareTerm(left.Term, right.Term),
                "ScoreValue" => left.ScoreValue.CompareTo(right.ScoreValue),
                "CreditText" => ParseDecimal(left.CreditText).CompareTo(ParseDecimal(right.CreditText)),
                "IsIncluded" => left.IsIncluded.CompareTo(right.IsIncluded),
                "SourceLabel" => left.Source.CompareTo(right.Source),
                "CourseCode" => StringComparer.CurrentCultureIgnoreCase.Compare(left.CourseCode, right.CourseCode),
                _ => StringComparer.CurrentCultureIgnoreCase.Compare(left.CourseName, right.CourseName),
            };
            return direction == ListSortDirection.Ascending ? result : -result;
        }

        private static int CompareTerm(string left, string right)
        {
            var hasLeft = AcademicTerm.TryParse(left, out var leftTerm);
            var hasRight = AcademicTerm.TryParse(right, out var rightTerm);
            return hasLeft && hasRight ? leftTerm.CompareTo(rightTerm) : StringComparer.CurrentCulture.Compare(left, right);
        }

        private static decimal ParseDecimal(string value)
            => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : decimal.MinValue;
    }

    private sealed record OverviewScopeOption(string Key, string DisplayName, int? AcademicYearStart, AcademicTerm? Term)
    {
        public static OverviewScopeOption All { get; } = new("all", "全部学期", null, null);

        public bool IsAll => Key == All.Key;

        public static OverviewScopeOption ForAcademicYear(int startYear)
            => new($"year:{startYear}", $"{startYear}-{startYear + 1} 学年", startYear, null);

        public static OverviewScopeOption ForTerm(AcademicTerm term)
            => new($"term:{term}", term.ToString(), null, term);

        public IEnumerable<CourseRecord> Filter(IEnumerable<CourseRecord> courses)
        {
            if (IsAll)
            {
                return courses;
            }

            if (Term is { } term)
            {
                return courses.Where(course => course.Term == term);
            }

            return AcademicYearStart is { } startYear
                ? courses.Where(course => course.Term.StartYear == startYear)
                : courses;
        }
    }
}
