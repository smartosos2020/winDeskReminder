using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using WinDeskReminder.Models;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace WinDeskReminder.Controls;

public sealed class SegmentedHandle : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(SegmentedHandle),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty DockEdgeProperty = DependencyProperty.Register(
        nameof(DockEdge),
        typeof(DockEdge),
        typeof(SegmentedHandle),
        new FrameworkPropertyMetadata(DockEdge.Right, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly WpfPen BorderPen = new(new SolidColorBrush(WpfColor.FromArgb(210, 229, 231, 235)), 1);
    private readonly List<ReminderItem> _trackedItems = [];
    private INotifyCollectionChanged? _trackedCollection;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DockEdge DockEdge
    {
        get => (DockEdge)GetValue(DockEdgeProperty);
        set => SetValue(DockEdgeProperty, value);
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        return new WpfSize(DesiredSizeForEdge(DockEdge).Width, DesiredSizeForEdge(DockEdge).Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var items = _trackedItems.Where(item => item.IsEnabled).ToList();
        if (items.Count == 0)
        {
            items.AddRange(_trackedItems.Take(1));
        }

        var radius = DockEdge is DockEdge.Left or DockEdge.Right
            ? ActualHeight / 2
            : ActualWidth / 2;
        if (radius <= 0)
        {
            return;
        }

        var center = GetCenter(radius);
        var (startAngle, totalSweep) = GetAngleRange();
        var segmentSweep = totalSweep / Math.Max(1, items.Count);

        var shell = CreateSectorGeometry(center, radius, startAngle, totalSweep);
        drawingContext.DrawGeometry(new SolidColorBrush(WpfColor.FromArgb(238, 248, 250, 252)), BorderPen, shell);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var segmentStart = startAngle + segmentSweep * i;
            var trackBrush = new SolidColorBrush(ColorWithAlpha(item.AccentColor, 42));
            var fillBrush = new SolidColorBrush(ColorWithAlpha(item.AccentColor, 224));

            drawingContext.DrawGeometry(
                trackBrush,
                null,
                CreateSectorGeometry(center, radius - 3, segmentStart + 1.4, segmentSweep - 2.8));

            var fillSweep = Math.Max(0, segmentSweep - 2.8) * Math.Clamp(item.ProgressPercent / 100, 0, 1);
            if (fillSweep > 0.4)
            {
                drawingContext.DrawGeometry(
                    fillBrush,
                    null,
                    CreateSectorGeometry(center, radius - 3, segmentStart + 1.4, fillSweep));
            }
        }

        drawingContext.DrawGeometry(null, BorderPen, shell);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var handle = (SegmentedHandle)dependencyObject;
        handle.TrackItems(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
    }

    private void TrackItems(IEnumerable? oldItems, IEnumerable? newItems)
    {
        if (_trackedCollection is not null)
        {
            _trackedCollection.CollectionChanged -= OnCollectionChanged;
            _trackedCollection = null;
        }

        foreach (var item in _trackedItems)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        _trackedItems.Clear();

        if (newItems is not null)
        {
            foreach (var item in newItems.OfType<ReminderItem>())
            {
                _trackedItems.Add(item);
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        if (newItems is INotifyCollectionChanged collection)
        {
            _trackedCollection = collection;
            _trackedCollection.CollectionChanged += OnCollectionChanged;
        }

        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TrackItems(null, ItemsSource);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReminderItem.ProgressPercent) or nameof(ReminderItem.AccentColor) or nameof(ReminderItem.IsEnabled))
        {
            InvalidateVisual();
        }
    }

    private WpfPoint GetCenter(double radius)
    {
        return DockEdge switch
        {
            DockEdge.Left => new WpfPoint(0, radius),
            DockEdge.Top => new WpfPoint(radius, 0),
            DockEdge.Bottom => new WpfPoint(radius, radius),
            _ => new WpfPoint(radius, radius)
        };
    }

    private (double StartAngle, double TotalSweep) GetAngleRange()
    {
        return DockEdge switch
        {
            DockEdge.Left => (-90, 180),
            DockEdge.Top => (0, 180),
            DockEdge.Bottom => (180, 180),
            _ => (90, 180)
        };
    }

    private static Geometry CreateSectorGeometry(WpfPoint center, double radius, double startAngle, double sweepAngle)
    {
        sweepAngle = Math.Clamp(sweepAngle, 0.01, 359.99);
        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweepAngle);

        var figure = new PathFigure
        {
            StartPoint = center,
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new LineSegment(start, true));
        figure.Segments.Add(new ArcSegment(
            end,
            new WpfSize(radius, radius),
            0,
            sweepAngle > 180,
            SweepDirection.Clockwise,
            true));
        figure.Segments.Add(new LineSegment(center, true));

        return new PathGeometry([figure]);
    }

    private static WpfPoint PointOnCircle(WpfPoint center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180;
        return new WpfPoint(
            center.X + Math.Cos(radians) * radius,
            center.Y + Math.Sin(radians) * radius);
    }

    private static WpfColor ColorWithAlpha(WpfColor color, byte alpha)
    {
        return WpfColor.FromArgb(alpha, color.R, color.G, color.B);
    }

    public static WpfSize DesiredSizeForEdge(DockEdge edge)
    {
        return edge is DockEdge.Left or DockEdge.Right
            ? new WpfSize(34, 68)
            : new WpfSize(68, 34);
    }
}
