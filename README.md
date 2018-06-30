# RichTextBoxRegexMatchBehaviour
A behaviour for a WPF `RichTextBox` that disables all user styling and instead applies custom styling to any text in the document that matches a regular expression.

    public sealed class RichTextBoxRegexMatchBehaviour : Behavior<RichTextBox>

The behaviour can be found in [Behaviours/RichTextBoxRegexMatchBehaviour.cs](Behaviours/RichTextBoxRegexMatchBehaviour.cs).

The required `System.Windows.Interactivity` namespace can be found in the [`System.Windows.Interactivity.WPF` NuGet package](https://www.nuget.org/packages/System.Windows.Interactivity.WPF).

## Example
The following example has two separate `RichTextBox`es and can be recreated using the [Example.xaml](Example.xaml) file.

![Example](Example.png)
