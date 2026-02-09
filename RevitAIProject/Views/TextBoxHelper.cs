using System.Windows;
using System.Windows.Controls;

namespace RevitAIProject.Views
{
    public static class TextBoxHelper
    {
        // 1. Свойство для отслеживания позиции курсора
        public static readonly DependencyProperty SelectionStartProperty =
            DependencyProperty.RegisterAttached("SelectionStart", typeof(int), typeof(TextBoxHelper),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static int GetSelectionStart(DependencyObject obj) => (int)obj.GetValue(SelectionStartProperty);
        public static void SetSelectionStart(DependencyObject obj, int value) => obj.SetValue(SelectionStartProperty, value);

        // 2. Свойство-команда: когда во ViewModel прилетает текст, TextBox его вставляет
        public static readonly DependencyProperty InsertTextProperty =
            DependencyProperty.RegisterAttached("InsertText", typeof(string), typeof(TextBoxHelper),
                new PropertyMetadata(null, OnInsertTextChanged));

        public static string GetInsertText(DependencyObject obj) => (string)obj.GetValue(InsertTextProperty);
        public static void SetInsertText(DependencyObject obj, string value) => obj.SetValue(InsertTextProperty, value);

        private static void OnInsertTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox && e.NewValue is string voiceText && !string.IsNullOrWhiteSpace(voiceText))
            {
                // Если поле пустое — просто заменяем весь текст
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = voiceText;
                    textBox.SelectionStart = textBox.Text.Length;
                }
                // Если курсор где-то внутри текста — вставляем
                else
                {
                    int start = textBox.SelectionStart;
                    string currentText = textBox.Text;

                    // Логика умных пробелов
                    string toInsert = voiceText;
                    if (start > 0 && currentText[start - 1] != ' ') toInsert = " " + toInsert;
                    if (start < currentText.Length && currentText[start] != ' ') toInsert += " ";

                    textBox.Text = currentText.Insert(start, toInsert);
                    textBox.SelectionStart = start + toInsert.Length;
                }

                textBox.Focus();
                SetInsertText(textBox, null); // Сброс триггера
            }
        }

        // Подписка на событие изменения курсора в реальном времени
        public static readonly DependencyProperty ObserveSelectionProperty =
            DependencyProperty.RegisterAttached("ObserveSelection", typeof(bool), typeof(TextBoxHelper),
                new PropertyMetadata(false, (d, e) => {
                    if (d is TextBox tb) tb.SelectionChanged += (s, ce) => SetSelectionStart(tb, tb.SelectionStart);
                }));

        public static bool GetObserveSelection(DependencyObject obj) => (bool)obj.GetValue(ObserveSelectionProperty);
        public static void SetObserveSelection(DependencyObject obj, bool value) => obj.SetValue(ObserveSelectionProperty, value);
    }
}
