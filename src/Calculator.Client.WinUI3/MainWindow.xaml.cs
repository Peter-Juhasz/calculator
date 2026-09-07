using Calculator.Expressions;
using Calculator.Expressions.WolframAlpha;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Xml;
using System.Xml.Linq;
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
    /// How long the typing has to stop for before what was typed is worked out. Long enough that
    /// a number being typed in is not read digit by digit, short enough that the result still
    /// reads as following the keystroke rather than a pause after it.
    /// </summary>
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The ways of working an expression out that are offered to the reader, in the order they
    /// are offered. The first is what the window starts on, so it is the one most expressions
    /// want.
    /// </summary>
    private readonly IReadOnlyList<IExpressionEvaluator> _evaluatorChoices;

    /// <summary>
    /// What the box holds, as of each keystroke. Nothing here is worked out yet — the readings
    /// this feeds are what settle which of these are.
    /// </summary>
    private readonly Subject<string> _expressions = new();

    /// <summary>
    /// Which arithmetic the reader has settled on, as of each time they change their mind.
    /// </summary>
    private readonly Subject<IExpressionEvaluator> _evaluators = new();

    /// <summary>
    /// The standing arrangement that turns those two into results on the line. Held so it can be
    /// let go of when the window closes.
    /// </summary>
    private readonly IDisposable _readings;

    public MainWindow(IEnumerable<IExpressionEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);

        _evaluatorChoices = [.. evaluators];

        InitializeComponent();
        ConfigureWindowChrome();

        // Before anything is put into either of them, so that the first evaluator and the first
        // keystroke are both seen.
        _readings = SubscribeToReadings();
        Closed += (_, _) => _readings.Dispose();

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
        EvaluatorSelector.ItemsSource = _evaluatorChoices;
        EvaluatorSelector.SelectedIndex = 0;
    }

    /// <summary>
    /// Arranges for what is typed to become what is on the result line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typing runs ahead of arithmetic, so the keystrokes are let settle first: nothing is worked
    /// out until the typing has paused, and an expression that comes back to what was last worked
    /// out — a character typed and rubbed out again — is not worked out a second time. A change of
    /// arithmetic is not made to wait like that, because it is one deliberate act rather than a
    /// run of them.
    /// </para>
    /// <para>
    /// Each reading supersedes the one before it: the arithmetic still under way when a newer
    /// expression arrives is called off, so only the newest ever reaches the line. That waiting
    /// and that arithmetic happen away from the UI thread, and the result is brought back to it
    /// to be shown.
    /// </para>
    /// </remarks>
    private IDisposable SubscribeToReadings()
    {
        var uiThread = SynchronizationContext.Current!;

        return _expressions
            .Throttle(TypingPause)
            .DistinctUntilChanged(StringComparer.Ordinal)
            .CombineLatest(_evaluators, (mathML, evaluator) => (MathML: mathML, Evaluator: evaluator))
            .Select(reading => Observable.FromAsync(cancellationToken =>
                ReadAsync(reading.MathML, reading.Evaluator, cancellationToken)))
            .Switch()
            .ObserveOn(uiThread)
            .Subscribe(Show);
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

    /// <summary>
    /// The box can only be read on the thread that owns it, so the markup is taken here and now;
    /// what it comes to is settled later, and elsewhere.
    /// </summary>
    private void InputBoxTextChanged(object sender, RoutedEventArgs e)
    {
        ReplaceAsteriskWithMultiplicationSign();

        InputBox.TextDocument.GetMathML(out var mathML);
        _expressions.OnNext(mathML);
    }

    /// <summary>
    /// The asterisk key is how multiplication is reached for on a keyboard, but math mode takes it
    /// for the asterisk operator, which is not how anyone writes multiplication down. What was
    /// meant is put on the screen instead, as soon as the key lands.
    /// </summary>
    /// <remarks>
    /// The keystroke is let through and mended afterwards rather than turned away: nothing in
    /// WinUI can refuse a character before the box has taken it — a key can be swallowed, but only
    /// by the key it is on, which is not the same key on every keyboard. Reading the character math
    /// mode settled on rather than the key that produced it leaves the layout out of it entirely.
    /// </remarks>
    private void ReplaceAsteriskWithMultiplicationSign()
    {
        var selection = InputBox.TextDocument.Selection;

        if (selection.StartPosition != selection.EndPosition || selection.StartPosition == 0)
        {
            return;
        }

        var typed = InputBox.TextDocument.GetRange(selection.StartPosition - 1, selection.StartPosition);

        if (typed.Text == "∗")
        {
            typed.Text = "×";
            selection.SetRange(typed.EndPosition, typed.EndPosition);
        }
    }

    /// <summary>
    /// The expression stands as it was typed; only the arithmetic behind it has changed. Working
    /// it out again is what makes the choice mean anything.
    /// </summary>
    private void EvaluatorSelectorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EvaluatorSelector.SelectedItem is IExpressionEvaluator evaluator)
        {
            _evaluators.OnNext(evaluator);
        }
    }

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
    /// Reads what the box handed over and works out what it comes to, or what stands in the way
    /// of an answer. Nothing here touches the window: it says what to show, and is shown by the
    /// thread that may.
    /// </summary>
    /// <remarks>
    /// The expression goes to the evaluator as the markup it arrived as, rather than flattened
    /// onto one line here first. What notation an expression may be written in depends on what is
    /// going to work it out — a root or a name means nothing to arithmetic done in a decimal, and
    /// a great deal to a service that does algebra — and that is not the window's to decide.
    /// </remarks>
    private static async Task<Reading> ReadAsync(
        string mathML,
        IExpressionEvaluator evaluator,
        CancellationToken cancellationToken)
    {
        try
        {
            if (ParseMathMl(mathML) is not { } expression)
            {
                return Reading.Nothing;
            }

            return Reading.Of(await evaluator.EvaluateAsync(expression, cancellationToken));
        }
        catch (ExpressionFormatException exception)
        {
            return Reading.Problem(exception.Message, isHint: exception.IsIncomplete);
        }
        // An evaluator that asks a service rather than working the answer out here can fail for
        // reasons that have nothing to do with the expression. That is worth saying plainly, and
        // in the colour a mistake gets, because the reader can do something about it.
        catch (WolframAlphaException exception)
        {
            return Reading.Problem(exception.Message, isHint: false);
        }
        catch (DivideByZeroException exception)
        {
            return Reading.Problem(exception.Message, isHint: false);
        }
        catch (OverflowException)
        {
            return Reading.Problem("The result is too large to work out.", isHint: false);
        }
        catch (ArithmeticException exception)
        {
            return Reading.Problem(exception.Message, isHint: false);
        }
    }

    /// <summary>
    /// Reads the markup the box handed back, or nothing at all where there was nothing in the box
    /// to read.
    /// </summary>
    /// <exception cref="ExpressionFormatException">The markup is not readable.</exception>
    private static XDocument? ParseMathMl(string mathML)
    {
        // The rich edit box pads what it hands back with a byte order mark and a terminator.
        var markup = mathML.Trim('\uFEFF', '\0').Trim();

        if (markup.Length == 0)
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(markup);

            // An empty box still hands back a document, the math element with nothing inside it,
            // and an expression not yet begun is not worth saying anything about.
            return document.Root is { HasElements: true } ? document : null;
        }
        catch (XmlException exception)
        {
            throw new ExpressionFormatException("The expression could not be read.", exception);
        }
    }

    private void Show(Reading reading)
    {
        if (reading.Value is { } value)
        {
            ShowValue(value);
        }
        else if (reading.Message is { } message)
        {
            ShowMessage(message, reading.IsHint);
        }
        else
        {
            ShowNothing();
        }
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

    /// <summary>
    /// What one reading of the box came to: a value for the line, something standing in the way
    /// of one, or nothing at all because there was nothing in the box to read.
    /// </summary>
    private readonly record struct Reading(string? Value, string? Message, bool IsHint)
    {
        public static Reading Nothing => default;

        public static Reading Of(string value) => new(value, null, false);

        public static Reading Problem(string message, bool isHint) => new(null, message, isHint);
    }
}
