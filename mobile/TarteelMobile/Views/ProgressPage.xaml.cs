using TarteelMobile.Services;
using TarteelMobile.ViewModels;

namespace TarteelMobile.Views;

public partial class ProgressPage : ContentPage
{
    public ProgressPage(ProgressViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        SizeChanged += OnPageSizeChanged;
    }

    private void OnAssignmentStartClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: TodayAssignment assignment } &&
            BindingContext is ProgressViewModel vm &&
            vm.StartAssignmentCommand.CanExecute(assignment))
        {
            vm.StartAssignmentCommand.Execute(assignment);
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        var compact = Width > 0 && Width < 900;
        SummaryGrid.RowDefinitions = compact
            ? new RowDefinitionCollection { new(GridLength.Auto), new(GridLength.Auto) }
            : new RowDefinitionCollection { new(GridLength.Auto) };
        SummaryGrid.ColumnDefinitions = compact
            ? new ColumnDefinitionCollection { new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(new GridLength(240)) };
        ProgressColumnsGrid.ColumnDefinitions = compact
            ? new ColumnDefinitionCollection { new(GridLength.Star) }
            : new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) };
        Grid.SetColumn(TodaySummaryBorder, 0);
        Grid.SetRow(TodaySummaryBorder, 0);
        Grid.SetColumn(CurriculumStack, compact ? 0 : 1);
        Grid.SetRow(CurriculumStack, compact ? 1 : 0);
        Grid.SetColumn(WeakVersesLayout, compact ? 0 : 1);
        Grid.SetRow(WeakVersesLayout, compact ? 1 : 0);
    }
}
