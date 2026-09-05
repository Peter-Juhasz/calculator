using System.Globalization;
using Calculator.Expressions;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace Calculator.Client.WinUI3;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The window opens wide rather than tall, and no bigger than the two lines it holds. An
    /// expression and its result each read along a line, and there is nothing else to stack
    /// underneath them.
    /// </summary>
    private static readonly SizeInt32 InitialSize = new(720, 380);

    /// <summary>
    /// Up to twelve decimals are shown, grouped for reading. Trailing zeros are dropped, so a
    /// whole number reads as one. A division that does not come out even carries far more digits
    /// than that, and is rounded to fit rather than run off the line.
    /// </summary>
    private const string ResultFormat = "#,##0.############";

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowChrome();

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

    private void InputBoxTextChanged(object sender, RoutedEventArgs e) => UpdateResult();

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
    /// makes the result follow the expression rather than wait for it.
    /// </summary>
    private void UpdateResult()
    {
        InputBox.TextDocument.GetMathML(out var mathML);

        try
        {
            var expression = MathMlLinearizer.Linearize(mathML);

            if (string.IsNullOrWhiteSpace(expression))
            {
                ShowNothing();
                return;
            }

            ShowValue(ExpressionEvaluator.Evaluate(expression));
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

    private void ShowNothing()
    {
        ResultText.Visibility = Visibility.Collapsed;
        MessageText.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Collapsed;
    }

    private void ShowValue(decimal value)
    {
        ResultText.Text = Format(value);
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

    private static string Format(decimal value) => value.ToString(ResultFormat, CultureInfo.CurrentCulture);
}
