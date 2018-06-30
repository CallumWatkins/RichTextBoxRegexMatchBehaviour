using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interactivity; // NuGet: System.Windows.Interactivity.WPF
using System.Windows.Threading;

namespace RTB.Behaviours
{
    /// <summary>
    /// A behaviour for a WPF RichTextBox that disables all user styling and instead applies custom styling to any text in the document that matches a regular expression.
    /// Rebuilds the document as a single paragraph with alternating Runs of text that do and don't match the regular expression, with matching Runs styled.
    /// </summary>
    public sealed class RichTextBoxRegexMatchBehaviour : Behavior<RichTextBox>
    {
        private Regex _regex;
        private DispatcherTimer _delayedTextChangedTimer;

        /// <summary>
        /// The regular expression pattern used to search for areas of the document to style.
        /// </summary>
        private static readonly DependencyProperty RegexPatternProperty = DependencyProperty.Register("RegexPattern", typeof(string),
                                                                                                      typeof(RichTextBoxRegexMatchBehaviour),
                                                                                                      new PropertyMetadata(default(string), RegexPatternPropertyChangedCallback));

        private static void RegexPatternPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behaviour = (RichTextBoxRegexMatchBehaviour)d;
            behaviour._regex = e.NewValue == null ? null : new Regex((string)e.NewValue, behaviour.RegexOptions);
        }

        /// <summary>
        /// Options for the regular expression engine.
        /// </summary>
        private static readonly DependencyProperty RegexOptionsProperty = DependencyProperty.Register("RegexOptions", typeof(RegexOptions),
                                                                                                      typeof(RichTextBoxRegexMatchBehaviour),
                                                                                                      new PropertyMetadata(RegexOptions.None, RegexOptionsPropertyChangedCallback));

        private static void RegexOptionsPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behaviour = (RichTextBoxRegexMatchBehaviour)d;
            if (behaviour._regex != null)
            {
                behaviour._regex = new Regex(behaviour._regex.ToString(), (RegexOptions)e.NewValue);
            }
        }

        /// <summary>
        /// The properties and their values that are applied to the sections of the document that match the regular expression.
        /// </summary>
        private static readonly DependencyProperty PropertyValuesProperty = DependencyProperty.Register("PropertyValues", typeof(PropertyValue[]),
                                                                                                        typeof(RichTextBoxRegexMatchBehaviour),
                                                                                                        new PropertyMetadata(default(PropertyValue[])));

        /// <summary>
        /// The minimum time (in milliseconds) after the last TextChanged event before the styling will be updated.
        /// Setting this property to zero will cause the document to be restyled immediately after each change to the document.
        /// Increasing this value can help improve performance on slower systems or larger documents where updating frequently is not desired.
        /// </summary>
        private static readonly DependencyProperty TextChangedDelayProperty = DependencyProperty.Register("TextChangedDelay", typeof(int),
                                                                                                          typeof(RichTextBoxRegexMatchBehaviour),
                                                                                                          new PropertyMetadata(default(int), TextChangedDelayPropertyChangedCallback));

        private static void TextChangedDelayPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behaviour = (RichTextBoxRegexMatchBehaviour)d;
            var newDelay = (int)e.NewValue;
            if (newDelay <= 0)
            {
                if (behaviour._delayedTextChangedTimer != null)
                {
                    behaviour._delayedTextChangedTimer.Tick -= behaviour.DelayedTextChangedTimerOnTick;
                    behaviour._delayedTextChangedTimer = null;
                }
            }
            else
            {
                behaviour._delayedTextChangedTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(newDelay)};
                behaviour._delayedTextChangedTimer.Tick += behaviour.DelayedTextChangedTimerOnTick;
            }
        }

        /// <summary>
        /// The regular expression pattern used to search for areas of the document to style.
        /// </summary>
        public string RegexPattern
        {
            get => (string)GetValue(RegexPatternProperty);
            set => SetValue(RegexPatternProperty, value);
        }

        /// <summary>
        /// Options for the regular expression engine.
        /// </summary>
        public RegexOptions RegexOptions
        {
            get => (RegexOptions)GetValue(RegexOptionsProperty);
            set => SetValue(RegexOptionsProperty, value);
        }

        /// <summary>
        /// The properties and their values that are applied to the sections of the document that match the regular expression.
        /// </summary>
        public PropertyValue[] PropertyValues
        {
            get => (PropertyValue[])GetValue(PropertyValuesProperty);
            set => SetValue(PropertyValuesProperty, value);
        }

        /// <summary>
        /// The minimum time (in milliseconds) after the last TextChanged event before the styling will be updated.
        /// Setting this property to zero will cause the document to be restyled immediately after each change to the document.
        /// Increasing this value can help improve performance on slower systems or larger documents where updating frequently is not desired.
        /// </summary>
        public int TextChangedDelay
        {
            get => (int)GetValue(TextChangedDelayProperty);
            set => SetValue(TextChangedDelayProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.TextChanged += RichTextBoxOnTextChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.TextChanged -= RichTextBoxOnTextChanged;
        }

        /// <summary>
        /// A TextChanged event occurred in the RichTextBox.
        /// </summary>
        private void RichTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextChangedDelay < 0)
            {
                // Manual only
            }
            else if (_delayedTextChangedTimer == null)
            {
                // No delay
                RestyleDocument();
            }
            else
            {
                // Reset the delay
                _delayedTextChangedTimer.Stop();
                _delayedTextChangedTimer.Start();
            }
        }

        /// <summary>
        /// The text changed delay timer reached the minimum delay.
        /// </summary>
        private void DelayedTextChangedTimerOnTick(object sender, EventArgs e)
        {
            _delayedTextChangedTimer.Stop();
            RestyleDocument();
        }

        /// <summary>
        /// Uses a heuristic approach to find the pointer to a position in a document specified by an offset of text elements; see <see cref="StringInfo.LengthInTextElements"/>.
        /// </summary>
        /// <param name="textElementOffset">The desired index (in text elements).</param>
        /// <param name="document">The document to find the pointer in.</param>
        private static TextPointer GetPointerFromTextElementOffset(int textElementOffset, FlowDocument document)
        {
            if (textElementOffset <= 0) { return document.ContentStart; }
            
            if (textElementOffset >= new StringInfo(new TextRange(document.ContentStart, document.ContentEnd).Text).LengthInTextElements) { return document.ContentEnd; }

            TextPointer currentPointer = document.ContentStart.GetPositionAtOffset(textElementOffset, LogicalDirection.Forward);
            // The pointer will not reach the desired location if any structure symbols are present
            // between the start of the document and the character index.
            int characterDifference = textElementOffset - new StringInfo(new TextRange(document.ContentStart, currentPointer).Text).LengthInTextElements;
            while (characterDifference != 0)
            {
                // ReSharper disable once PossibleNullReferenceException
                currentPointer = currentPointer.GetPositionAtOffset(characterDifference, LogicalDirection.Forward);
                characterDifference = textElementOffset - new StringInfo(new TextRange(document.ContentStart, currentPointer).Text).LengthInTextElements;

                if (characterDifference < 0)
                {
                    return document.ContentEnd;
                }
            }

            return currentPointer;
        }

        /// <summary>
        /// Rebuild the document without any existing styles. Apply styling to text that matches the regular expression.
        /// </summary>
        public void RestyleDocument()
        {
            // Remove the TextChanged event handler to prevent the changes made here from triggering it
            AssociatedObject.TextChanged -= RichTextBoxOnTextChanged;

            string allText = new TextRange(AssociatedObject.Document.ContentStart, AssociatedObject.Document.ContentEnd).Text;
            if (allText.Length != 0)
            {
                // Store the location of the caret and the current scroll offsets
                int caretOffset = new StringInfo(new TextRange(AssociatedObject.Document.ContentStart, AssociatedObject.CaretPosition).Text).LengthInTextElements;
                double verticalScrollOffset = AssociatedObject.VerticalOffset;
                double horizontalScrollOffset = AssociatedObject.HorizontalOffset;

                // Paragraph objects apply a newline sequence to the end of all documents
                if (allText.EndsWith("\r\n"))
                {
                    allText = allText.Substring(0, allText.Length - 2);
                    if (AssociatedObject.CaretPosition.CompareTo(AssociatedObject.Document.ContentEnd) == 0) { caretOffset -= 2; }
                }

                MatchCollection matches = _regex.Matches(allText);

                var paragraph = new Paragraph();
                if (matches.Count == 0)
                {
                    paragraph.Inlines.Add(allText);
                }
                else
                {
                    IEnumerator matchesEnumerator = matches.GetEnumerator();
                    matchesEnumerator.MoveNext();
                    var nextMatch = (Match)matchesEnumerator.Current;

                    // Rebuild the document by alternating between Run objects for matches and non-matches
                    for (int i = 0;;)
                    {
                        if (nextMatch.Index == i) // Match
                        {
                            // Create a new match run
                            var run = new Run(nextMatch.Value);
                            // Apply styling to the match Run
                            foreach (PropertyValue p in PropertyValues)
                            {
                                run.SetValue(p.Property, p.Value);
                            }

                            paragraph.Inlines.Add(run);
                            i += nextMatch.Length;
                            if (!matchesEnumerator.MoveNext())
                            {
                                // End of matches reached. Append any remaining text.
                                if (i < allText.Length)
                                {
                                    paragraph.Inlines.Add(allText.Substring(i));
                                }

                                break;
                            }

                            nextMatch = (Match)matchesEnumerator.Current;
                        }
                        else // Non-match
                        {
                            int nonMatchLength = nextMatch.Index - i;
                            paragraph.Inlines.Add(allText.Substring(i, nonMatchLength));
                            i += nonMatchLength;
                        }
                    }
                }

                // Replace the old document content with the new content
                AssociatedObject.Document.Blocks.Clear();
                AssociatedObject.Document.Blocks.Add(paragraph);

                // Set the caret position to its original location
                AssociatedObject.CaretPosition = GetPointerFromTextElementOffset(caretOffset, AssociatedObject.Document);

                // Scroll the document to the original offset
                AssociatedObject.ScrollToHorizontalOffset(horizontalScrollOffset);
                AssociatedObject.ScrollToVerticalOffset(verticalScrollOffset);
            }

            // Reattach the TextChanged event handler
            AssociatedObject.TextChanged += RichTextBoxOnTextChanged;
        }
    }
}
