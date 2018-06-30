using System.Windows;

namespace RTB.Behaviours
{
    /// <summary>
    /// Represents a dependency property and a corresponding value to be assigned to it.
    /// </summary>
    public class PropertyValue
    {
        public DependencyProperty Property { get; set; }
        public object Value { get; set; }

        public PropertyValue()
        {
        }

        public PropertyValue(DependencyProperty dependencyProperty, object value)
        {
            Property = dependencyProperty;
            Value = value;
        }

        public static implicit operator PropertyValue((DependencyProperty, object) t)
        {
            return new PropertyValue(t.Item1, t.Item2);
        }
    }
}
