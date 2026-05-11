using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WpfApp5;

public partial class MainWindow : Window
{
    private const string DefaultDisplayText = "0";
    private const string ErrorPrefix = "Ошибка: ";
    private const double NearZeroEpsilon = 1e-12;

    private static readonly Brush NormalDisplayBrush = Brushes.White;
    private static readonly Brush ErrorDisplayBrush = new SolidColorBrush(Color.FromRgb(255, 204, 204));

    private bool _hasError;
    private bool _resultShown;
    private double? _memoryValue;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Keyboard.Focus(this);
    }

    private void InputButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string value })
        {
            return;
        }

        ProcessInput(value);
    }

    private void EqualsButton_Click(object sender, RoutedEventArgs e)
    {
        EvaluateCurrentExpression();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearDisplay(animate: true);
    }

    private void BackspaceButton_Click(object sender, RoutedEventArgs e)
    {
        Backspace();
    }

    private void MemoryClearButton_Click(object sender, RoutedEventArgs e)
    {
        _memoryValue = null;
        AddHistory("MC");
    }

    private void MemoryRecallButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_memoryValue.HasValue)
        {
            AddHistory("MR: память пуста");
            return;
        }

        _hasError = false;
        _resultShown = true;
        SetDisplayBrush(isError: false);
        AnimateDisplayTextChange(FormatNumber(_memoryValue.Value), fadeTo: 0.2, totalDurationMs: 220);
        AddHistory($"MR = {FormatNumber(_memoryValue.Value)}");
    }

    private void MemoryAddButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyMemoryOperation(isAddition: true);
    }

    private void MemorySubtractButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyMemoryOperation(isAddition: false);
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (e.Text == "=")
        {
            EvaluateCurrentExpression();
            e.Handled = true;
            return;
        }

        string? normalizedInput = NormalizeTextInput(e.Text);

        if (normalizedInput is null)
        {
            return;
        }

        ProcessInput(normalizedInput);
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            EvaluateCurrentExpression();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearDisplay(animate: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back)
        {
            Backspace();
            e.Handled = true;
            return;
        }

        if (TryMapKeyToInput(e.Key, out string? value) && value is not null)
        {
            ProcessInput(value);
            e.Handled = true;
        }
    }

    private void ProcessInput(string value)
    {
        if (_hasError)
        {
            ClearDisplay(animate: false);
        }

        if (_resultShown && !IsOperator(value))
        {
            ClearDisplay(animate: false);
        }

        StopDisplayAnimation();
        SetDisplayBrush(isError: false);

        if (IsOperator(value))
        {
            HandleOperatorInput(value);
        }
        else if (value is "(" or ")")
        {
            HandleParenthesisInput(value);
        }
        else if (value == ",")
        {
            AppendDecimalSeparator();
        }
        else
        {
            AppendDigit(value);
        }

        _resultShown = false;
    }

    private void AppendDigit(string value)
    {
        string text = DisplayTextBox.Text;

        if (text == DefaultDisplayText)
        {
            if (value == "0")
            {
                return;
            }

            DisplayTextBox.Text = value;
            return;
        }

        if (text[^1] == ')')
        {
            return;
        }

        if (IsOperator(text[^1]) || text[^1] == '(')
        {
            DisplayTextBox.Text += value;
            return;
        }

        int currentNumberStart = GetCurrentNumberStart(text);
        string currentNumber = text[currentNumberStart..];

        if (currentNumber is "0" or "-0")
        {
            if (value == "0")
            {
                return;
            }

            string prefix = text[..currentNumberStart];
            string sign = currentNumber.StartsWith('-') ? "-" : string.Empty;
            DisplayTextBox.Text = prefix + sign + value;
            return;
        }

        DisplayTextBox.Text += value;
    }

    private void AppendDecimalSeparator()
    {
        string text = DisplayTextBox.Text;

        if (text == DefaultDisplayText)
        {
            DisplayTextBox.Text = "0,";
            return;
        }

        if (text == "-")
        {
            DisplayTextBox.Text = "-0,";
            return;
        }

        char lastChar = text[^1];

        if (lastChar == ')')
        {
            return;
        }

        if (IsOperator(lastChar) || lastChar == '(')
        {
            DisplayTextBox.Text += "0,";
            return;
        }

        if (lastChar == ',' || CurrentNumberHasDecimalSeparator(text))
        {
            return;
        }

        DisplayTextBox.Text += ",";
    }

    private void HandleOperatorInput(string value)
    {
        string text = DisplayTextBox.Text;

        if (text == DefaultDisplayText)
        {
            if (value == "-")
            {
                DisplayTextBox.Text = value;
            }

            return;
        }

        char lastChar = text[^1];

        if (char.IsDigit(lastChar) || lastChar == ')')
        {
            DisplayTextBox.Text += value;
            return;
        }

        if (lastChar == ',')
        {
            return;
        }

        if (lastChar == '(')
        {
            if (value == "-")
            {
                DisplayTextBox.Text += value;
            }

            return;
        }

        if (IsOperator(lastChar))
        {
            if (EndsWithUnaryMinus(text))
            {
                return;
            }

            DisplayTextBox.Text = text[..^1] + value;
        }
    }

    private void HandleParenthesisInput(string value)
    {
        if (value == "(")
        {
            AppendOpeningParenthesis();
            return;
        }

        AppendClosingParenthesis();
    }

    private void AppendOpeningParenthesis()
    {
        string text = DisplayTextBox.Text;

        if (text == DefaultDisplayText)
        {
            DisplayTextBox.Text = "(";
            return;
        }

        if (EndsWithUnaryMinus(text))
        {
            return;
        }

        char lastChar = text[^1];

        if (IsOperator(lastChar) || lastChar == '(')
        {
            DisplayTextBox.Text += "(";
        }
    }

    private void AppendClosingParenthesis()
    {
        string text = DisplayTextBox.Text;

        if (GetParenthesisBalance(text) <= 0)
        {
            return;
        }

        char lastChar = text[^1];

        if (char.IsDigit(lastChar) || lastChar == ')')
        {
            DisplayTextBox.Text += ")";
        }
    }

    private void Backspace()
    {
        if (_hasError || _resultShown)
        {
            ClearDisplay(animate: false);
            return;
        }

        StopDisplayAnimation();
        string text = DisplayTextBox.Text;

        if (text.Length <= 1 || (text.Length == 2 && text.StartsWith('-')))
        {
            DisplayTextBox.Text = DefaultDisplayText;
            return;
        }

        DisplayTextBox.Text = text[..^1];
    }

    private void EvaluateCurrentExpression()
    {
        if (_hasError)
        {
            ClearDisplay(animate: false);
            return;
        }

        string expression = DisplayTextBox.Text;

        try
        {
            double result = ExpressionCalculator.Calculate(expression);
            string formattedResult = FormatNumber(result);

            SetDisplayBrush(isError: false);
            AnimateDisplayTextChange(formattedResult, fadeTo: 0.15, totalDurationMs: 260);
            AddHistory($"{expression} = {formattedResult}");

            _hasError = false;
            _resultShown = true;
        }
        catch (CalculatorException exception)
        {
            ShowError(exception.Message);
        }
    }

    private void ApplyMemoryOperation(bool isAddition)
    {
        if (!TryGetCurrentValue(out double value))
        {
            return;
        }

        double memory = _memoryValue ?? 0;
        memory = isAddition ? memory + value : memory - value;
        _memoryValue = memory;

        string operation = isAddition ? "M+" : "M-";
        AddHistory($"{operation} {FormatNumber(value)} -> {FormatNumber(memory)}");
    }

    private bool TryGetCurrentValue(out double value)
    {
        value = 0;

        if (_hasError)
        {
            return false;
        }

        try
        {
            value = ExpressionCalculator.Calculate(DisplayTextBox.Text);
            return true;
        }
        catch (CalculatorException exception)
        {
            ShowError(exception.Message);
            return false;
        }
    }

    private void ClearDisplay(bool animate)
    {
        _hasError = false;
        _resultShown = false;
        SetDisplayBrush(isError: false);

        if (animate)
        {
            AnimateDisplayTextChange(DefaultDisplayText, fadeTo: 0.05, totalDurationMs: 220);
            return;
        }

        StopDisplayAnimation();
        DisplayTextBox.Text = DefaultDisplayText;
    }

    private void ShowError(string message)
    {
        _hasError = true;
        _resultShown = false;
        SetDisplayBrush(isError: true);
        AnimateDisplayTextChange($"{ErrorPrefix}{message}", fadeTo: 0.2, totalDurationMs: 220);
        AddHistory($"Ошибка: {message}");
    }

    private void AddHistory(string entry)
    {
        HistoryListBox.Items.Insert(0, entry);

        while (HistoryListBox.Items.Count > 20)
        {
            HistoryListBox.Items.RemoveAt(HistoryListBox.Items.Count - 1);
        }
    }

    private void AnimateDisplayTextChange(string newText, double fadeTo, int totalDurationMs)
    {
        StopDisplayAnimation();
        DisplayTextBox.Text = newText;
        DisplayTextBox.Opacity = fadeTo;

        Storyboard storyboard = new();
        DoubleAnimation fadeIn = new()
        {
            From = fadeTo,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(Math.Max(140, totalDurationMs))
        };

        Storyboard.SetTarget(fadeIn, DisplayTextBox);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeIn);
        storyboard.Begin();
    }

    private void StopDisplayAnimation()
    {
        DisplayTextBox.BeginAnimation(UIElement.OpacityProperty, null);
        DisplayTextBox.Opacity = 1;
    }

    private void SetDisplayBrush(bool isError)
    {
        DisplayTextBox.Foreground = isError ? ErrorDisplayBrush : NormalDisplayBrush;
    }

    private static int GetParenthesisBalance(string text)
    {
        int balance = 0;

        foreach (char current in text)
        {
            if (current == '(')
            {
                balance++;
            }
            else if (current == ')')
            {
                balance--;
            }
        }

        return balance;
    }

    private static int GetCurrentNumberStart(string text)
    {
        int index = text.Length - 1;

        while (index >= 0)
        {
            char current = text[index];

            if (char.IsDigit(current) || current == ',')
            {
                index--;
                continue;
            }

            if (current == '-' && (index == 0 || text[index - 1] == '('))
            {
                return index;
            }

            break;
        }

        return index + 1;
    }

    private static bool CurrentNumberHasDecimalSeparator(string text)
    {
        for (int index = text.Length - 1; index >= 0; index--)
        {
            char current = text[index];

            if (current == ',')
            {
                return true;
            }

            if (IsOperator(current) || current == '(' || current == ')')
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsOperator(string value)
    {
        return value is "+" or "-" or "*" or "/";
    }

    private static bool IsOperator(char value)
    {
        return value is '+' or '-' or '*' or '/';
    }

    private static bool EndsWithUnaryMinus(string text)
    {
        if (string.IsNullOrEmpty(text) || text[^1] != '-')
        {
            return false;
        }

        return text.Length == 1 || text[^2] == '(';
    }

    private static string FormatNumber(double number)
    {
        return number.ToString("G12", CultureInfo.CurrentCulture);
    }

    private static string? NormalizeTextInput(string text)
    {
        return text switch
        {
            "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" => text,
            "+" or "-" or "*" or "/" or "(" or ")" => text,
            "," or "." => ",",
            _ => null
        };
    }

    private static bool TryMapKeyToInput(Key key, out string? value)
    {
        bool shiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (shiftPressed)
        {
            value = key switch
            {
                Key.D8 => "*",
                Key.D9 => "(",
                Key.D0 => ")",
                _ => null
            };

            if (value is not null)
            {
                return true;
            }
        }

        value = key switch
        {
            Key.Add => "+",
            Key.Subtract => "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            Key.Decimal => ",",
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemQuestion => "/",
            _ => null
        };

        return value is not null;
    }

    private static class ExpressionCalculator
    {
        public static double Calculate(string expression)
        {
            List<Token> tokens = Tokenize(expression);
            List<Token> postfixTokens = ConvertToPostfix(tokens);
            return EvaluatePostfix(postfixTokens);
        }

        private static List<Token> Tokenize(string expression)
        {
            List<Token> tokens = new();
            bool expectNumber = true;

            for (int index = 0; index < expression.Length; index++)
            {
                char current = expression[index];

                if (char.IsWhiteSpace(current))
                {
                    continue;
                }

                if (char.IsDigit(current) || current is ',' or '.' || (current == '-' && expectNumber))
                {
                    tokens.Add(ReadNumber(expression, ref index, expectNumber));
                    expectNumber = false;
                    continue;
                }

                if (IsOperator(current))
                {
                    if (expectNumber)
                    {
                        throw new CalculatorException("оператор стоит не на своем месте");
                    }

                    tokens.Add(new Token(TokenType.Operator, current.ToString()));
                    expectNumber = true;
                    continue;
                }

                if (current == '(')
                {
                    tokens.Add(new Token(TokenType.LeftParenthesis, current.ToString()));
                    expectNumber = true;
                    continue;
                }

                if (current == ')')
                {
                    if (expectNumber)
                    {
                        throw new CalculatorException("пустые скобки или лишний оператор");
                    }

                    tokens.Add(new Token(TokenType.RightParenthesis, current.ToString()));
                    expectNumber = false;
                    continue;
                }

                throw new CalculatorException("обнаружен недопустимый символ");
            }

            if (tokens.Count == 0)
            {
                throw new CalculatorException("выражение пустое");
            }

            if (expectNumber)
            {
                throw new CalculatorException("выражение не закончено");
            }

            return tokens;
        }

        private static Token ReadNumber(string expression, ref int index, bool canReadSign)
        {
            int startIndex = index;
            bool hasDecimalSeparator = false;
            bool hasDigit = false;

            if (expression[index] == '-' && canReadSign)
            {
                index++;
            }

            while (index < expression.Length)
            {
                char current = expression[index];

                if (char.IsDigit(current))
                {
                    hasDigit = true;
                    index++;
                    continue;
                }

                if (current is ',' or '.')
                {
                    if (hasDecimalSeparator)
                    {
                        throw new CalculatorException("в числе несколько десятичных разделителей");
                    }

                    hasDecimalSeparator = true;
                    index++;
                    continue;
                }

                break;
            }

            if (!hasDigit)
            {
                throw new CalculatorException("после минуса должно быть число");
            }

            string text = expression[startIndex..index].Replace(',', '.');

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw new CalculatorException("число записано некорректно");
            }

            index--;
            return new Token(TokenType.Number, text, value);
        }

        private static List<Token> ConvertToPostfix(List<Token> tokens)
        {
            List<Token> output = new();
            Stack<Token> operators = new();

            foreach (Token token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                        output.Add(token);
                        break;

                    case TokenType.Operator:
                        while (operators.Count > 0
                               && operators.Peek().Type == TokenType.Operator
                               && GetPriority(operators.Peek().Text) >= GetPriority(token.Text))
                        {
                            output.Add(operators.Pop());
                        }

                        operators.Push(token);
                        break;

                    case TokenType.LeftParenthesis:
                        operators.Push(token);
                        break;

                    case TokenType.RightParenthesis:
                        MoveOperatorsUntilLeftParenthesis(output, operators);
                        break;
                }
            }

            while (operators.Count > 0)
            {
                Token token = operators.Pop();

                if (token.Type == TokenType.LeftParenthesis)
                {
                    throw new CalculatorException("скобки расставлены неверно");
                }

                output.Add(token);
            }

            return output;
        }

        private static void MoveOperatorsUntilLeftParenthesis(List<Token> output, Stack<Token> operators)
        {
            while (operators.Count > 0 && operators.Peek().Type != TokenType.LeftParenthesis)
            {
                output.Add(operators.Pop());
            }

            if (operators.Count == 0)
            {
                throw new CalculatorException("скобки расставлены неверно");
            }

            operators.Pop();
        }

        private static double EvaluatePostfix(List<Token> tokens)
        {
            Stack<double> numbers = new();

            foreach (Token token in tokens)
            {
                if (token.Type == TokenType.Number)
                {
                    numbers.Push(token.Value);
                    continue;
                }

                if (numbers.Count < 2)
                {
                    throw new CalculatorException("выражение записано некорректно");
                }

                double right = numbers.Pop();
                double left = numbers.Pop();
                numbers.Push(ApplyOperator(left, right, token.Text));
            }

            if (numbers.Count != 1)
            {
                throw new CalculatorException("выражение записано некорректно");
            }

            return numbers.Pop();
        }

        private static double ApplyOperator(double left, double right, string operatorText)
        {
            return operatorText switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" when Math.Abs(right) < NearZeroEpsilon => throw new CalculatorException("деление на ноль"),
                "/" => left / right,
                _ => throw new CalculatorException("неизвестная операция")
            };
        }

        private static int GetPriority(string operatorText)
        {
            return operatorText is "*" or "/" ? 2 : 1;
        }
    }

    private sealed class CalculatorException(string message) : Exception(message);

    private enum TokenType
    {
        Number,
        Operator,
        LeftParenthesis,
        RightParenthesis
    }

    private sealed record Token(TokenType Type, string Text, double Value = 0);
}

