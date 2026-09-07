using Calculator.Expressions;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace Calculator.Client.WinUI3;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The window opens wide rather than tall, and no bigger than what it holds. An expression
    /// and its result each read along a line, and above the result there is only the one control
    /// saying which arithmetic it was worked out in.
    /// </summary>
    private static readonly SizeInt32 InitialSize = new(720, 430);

    /// <summary>
    /// The ways of working an expression out that are offered to the reader, in the order they
    /// are offered. The first is what the window starts on, so it is the one most expressions
    /// want.
    /// </summary>
    private static readonly IExpressionEvaluator[] Evaluators =
    [
        new DecimalExpressionEvaluator(),
        new BigIntegerExpressionEvaluator(),
    ];

    /// <summary>
    /// Cancels the reading that is currently under way. Every keystroke supersedes the one before
    /// it, so an answer that is still being worked out when the expression changes is no longer
    /// wanted.
    /// </summary>
    private CancellationTokenSource _evaluation = new();

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChrome();
        ConfigureEvaluators();

        // Setting the math mode empties the box, so it happens before anything is in it. From
        // here on the box takes UnicodeMath — 1/2 builds a fraction, and so on.
        InputBox.TextDocument.SetMathMode(RichEditMathMode.MathOnly);

        ShowNothing();
    }

    private void ConfigureWindowChrome()
    {
        Title = "Calculator";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }

        AppWindow.Resize(InitialSize);
        CenterOnDisplay();
    }

    /// <summary>
    /// Fills the selector and settles on the first of the evaluators, so the window always has
    /// one in hand before there is anything to work out.
    /// </summary>
    private void ConfigureEvaluators()
    {
        EvaluatorSelector.ItemsSource = Evaluators;
        EvaluatorSelector.SelectedIndex = 0;
    }

    private void CenterOnDisplay()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

        AppWindow.Move(new PointInt32(
            workArea.X + ((workArea.Width - AppWindow.Size.Width) / 2),
            workArea.Y + ((workArea.Height - AppWindow.Size.Height) / 2)));
    }

    /// <summary>
    /// The box is the only thing here to type into, so it starts out holding the caret and the
    /// window can be typed into the moment it opens.
    /// </summary>
    private void RootGridLoaded(object sender, RoutedEventArgs e) => InputBox.Focus(FocusState.Programmatic);

    private async void InputBoxTextChanged(object sender, RoutedEventArgs e) => await UpdateResultAsync();

    /// <summary>
    /// The expression stands as it was typed; only the arithmetic behind it has changed. Working
    /// it out again is what makes the choice mean anything.
    /// </summary>
    private async void EvaluatorSelectorSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await UpdateResultAsync();

    /// <summary>
    /// A rich edit box would take Enter as a new line. An expression is one line, and its result
    /// is already on screen without having to be asked for.
    /// </summary>
    private void InputBoxPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Reads the box and works out what it comes to. Called on every keystroke, which is what
    /// makes the result follow the expression rather than wait for it, and again whenever the
    /// arithmetic behind it is changed.
    /// </summary>
    private async Task UpdateResultAsync()
    {
        var cancellationToken = SupersedeEvaluation();

        InputBox.TextDocument.GetMathML(out var mathML);

        try
        {
            var expression = MathMlLinearizer.Linearize(mathML);

            if (string.IsNullOrWhiteSpace(expression) || EvaluatorSelector.SelectedItem is not IExpressionEvaluator evaluator)
            {
                ShowNothing();
                return;
            }

            ShowValue(await evaluator.EvaluateAsync(expression, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // The expression moved on while this reading was under way. Whatever superseded it is
            // already on its way to the line, so nothing is shown for this one.
        }
        catch (ExpressionFormatException exception)
        {
            ShowMessage(exception.Message, isHint: exception.IsIncomplete);
        }
        catch (DivideByZeroException exception)
        {
            ShowMessage(exception.Message, isHint: false);
        }
        catch (OverflowException)
        {
            ShowMessage("The result is too large to work out.", isHint: false);
        }
        catch (ArithmeticException exception)
        {
            ShowMessage(exception.Message, isHint: false);
        }
    }

    /// <summary>
    /// Calls off the reading that is under way, if there is one, and hands back the token that
    /// stands for the reading taking its place.
    /// </summary>
    private CancellationToken SupersedeEvaluation()
    {
        var superseded = _evaluation;
        _evaluation = new CancellationTokenSource();

        superseded.Cancel();
        superseded.Dispose();

        return _evaluation.Token;
    }

    private void ShowNothing()
    {
        ResultText.Visibility = Visibility.Collapsed;
        MessageText.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Collapsed;
    }

    private void ShowValue(string value)
    {
        ResultText.Text = value;
        ResultText.Visibility = Visibility.Visible;
        MessageText.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Shows what stands in the way of a result. An expression that is merely unfinished is said
    /// quietly, because that is what every expression looks like while it is being typed; a
    /// genuine mistake is said in the colour reserved for one.
    /// </summary>
    private void ShowMessage(string message, bool isHint)
    {
        ResultText.Visibility = Visibility.Collapsed;
        MessageText.Text = message;
        MessageText.Foreground = (Brush)RootGrid.Resources[isHint ? "MessageHintBrush" : "MessageErrorBrush"];
        MessageText.Visibility = Visibility.Visible;
        CopyResultButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Puts the value currently on the result line onto the clipboard. There is nothing to read
    /// this back from within the app — the box does not accept plain numbers as math input — so
    /// this is purely for pasting the answer somewhere else.
    /// </summary>
    private void CopyResultButtonClick(object sender, RoutedEventArgs e)
    {
        var content = new DataPackage();
        content.SetText(ResultText.Text);
        Clipboard.SetContent(content);
    }
}
