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

    /// <summary>
    /// The markup currently on the result line, which is what the copy button copies. The box it
    /// is set into cannot be asked for it back as it went in — what comes out of a rich edit box
    /// is the markup of what it drew, not the markup it was given.
    /// </summary>
    private XDocument? _result;

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

        // The result box is never typed into, but it is set from MathML, and only a box in math
        // mode builds what that markup describes rather than spelling it out.
        ResultBox.TextDocument.SetMathMode(RichEditMathMode.MathOnly);

        // A math zone is typeset by Rich Edit's own equation renderer, which does not follow along
        // when the theme the box's Foreground is bound to changes underneath it. Colouring it
        // again is what keeps the answer visible after a light-to-dark switch, not just at first.
        RootGrid.ActualThemeChanged += (_, _) => ApplyResultForeground();

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
        _result = null;
        ResultBox.Visibility = Visibility.Collapsed;
        MessageText.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Puts the answer on the line as the markup it is, so that what was worked out is drawn the
    /// way it would be written rather than flattened into a sentence about itself.
    /// </summary>
    /// <remarks>
    /// The box is read-only so that nobody types into it, and read-only is exactly what stops the
    /// answer being put in as well, so it is lifted for the one call that sets it. The formatting
    /// is dropped on the way in: whitespace between elements is whitespace in the expression as
    /// far as a rich edit box is concerned.
    /// </remarks>
    private void ShowValue(XDocument value)
    {
        _result = value;

        ResultBox.IsReadOnly = false;
        ResultBox.TextDocument.SetMathML(value.ToString(SaveOptions.DisableFormatting));
        ApplyResultForeground();
        ResultBox.IsReadOnly = true;

        ResultBox.Visibility = Visibility.Visible;
        MessageText.Visibility = Visibility.Collapsed;
        CopyResultButton.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Colours the answer in the theme's ordinary text colour.
    /// </summary>
    /// <remarks>
    /// A math zone is drawn by Rich Edit's own equation renderer rather than by the ordinary
    /// character formatting a typed line would pick up, and it is given a black glyph colour by
    /// default regardless of what the box's <see cref="Control.Foreground"/> says — which on a
    /// dark theme leaves the answer unreadable against the surface behind it. Colouring the whole
    /// range once the markup is in, in the same brush the box is themed with, is what makes the
    /// answer follow the theme instead of standing apart from it.
    /// </remarks>
    private void ApplyResultForeground()
    {
        if (ResultBox.Foreground is not SolidColorBrush brush)
        {
            return;
        }

        var range = ResultBox.TextDocument.GetRange(0, TextConstants.MaxUnitCount);
        range.CharacterFormat.ForegroundColor = brush.Color;
    }

    /// <summary>
    /// Shows what stands in the way of a result. An expression that is merely unfinished is said
    /// quietly, because that is what every expression looks like while it is being typed; a
    /// genuine mistake is said in the colour reserved for one.
    /// </summary>
    private void ShowMessage(string message, bool isHint)
    {
        _result = null;
        ResultBox.Visibility = Visibility.Collapsed;
        MessageText.Text = message;
        MessageText.Foreground = (Brush)RootGrid.Resources[isHint ? "MessageHintBrush" : "MessageErrorBrush"];
        MessageText.Visibility = Visibility.Visible;
        CopyResultButton.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Puts the value currently on the result line onto the clipboard.
    /// </summary>
    /// <remarks>
    /// An answer that is a bare number goes over as that number, which is what anywhere else would
    /// want of it — a spreadsheet cell, a message, the box this was typed into. An answer that is
    /// anything more than a number has no such plain reading, and goes over as the markup of what
    /// is on the line, which is the only thing that keeps it whole.
    /// </remarks>
    private void CopyResultButtonClick(object sender, RoutedEventArgs e)
    {
        if (_result is not { } result)
        {
            return;
        }

        var content = new DataPackage();
        content.SetText(MathML.AsNumber(result) ?? result.ToString(SaveOptions.DisableFormatting));
        Clipboard.SetContent(content);
    }

    /// <summary>
    /// What one reading of the box came to: a value for the line, something standing in the way
    /// of one, or nothing at all because there was nothing in the box to read.
    /// </summary>
    private readonly record struct Reading(XDocument? Value, string? Message, bool IsHint)
    {
        public static Reading Nothing => default;

        public static Reading Of(XDocument value) => new(value, null, false);

        public static Reading Problem(string message, bool isHint) => new(null, message, isHint);
    }
}
