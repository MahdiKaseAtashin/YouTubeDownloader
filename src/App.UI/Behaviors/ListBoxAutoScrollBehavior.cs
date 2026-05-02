using System.Collections.Specialized;
using Microsoft.Xaml.Behaviors;
using WpfListBox = System.Windows.Controls.ListBox;

namespace App.UI.Behaviors;

public sealed class ListBoxAutoScrollBehavior : Behavior<WpfListBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.Loaded -= OnLoaded;
        }

        Unsubscribe();
        base.OnDetaching();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        AssociatedObject!.Loaded -= OnLoaded;
        Subscribe();
    }

    private INotifyCollectionChanged? _notifier;

    private void Subscribe()
    {
        Unsubscribe();
        _notifier = AssociatedObject?.Items;
        if (_notifier is not null)
        {
            _notifier.CollectionChanged += OnCollectionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_notifier is not null)
        {
            _notifier.CollectionChanged -= OnCollectionChanged;
            _notifier = null;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AssociatedObject is null || AssociatedObject.Items.Count == 0)
        {
            return;
        }

        var last = AssociatedObject.Items[^1];
        AssociatedObject.Dispatcher.BeginInvoke(
            () => AssociatedObject.ScrollIntoView(last),
            System.Windows.Threading.DispatcherPriority.Background);
    }
}
