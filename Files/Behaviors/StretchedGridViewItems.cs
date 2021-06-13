using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Files.Behaviors
{
    public class StretchedGridViewItems
    {
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.RegisterAttached("MinItemWidth", typeof(double),
            typeof(StretchedGridViewItems), new PropertyMetadata(0.0d, OnMinItemWidthChanged));

        public static readonly DependencyProperty FillBeforeWrapProperty =
           DependencyProperty.RegisterAttached("FillBeforeWrap", typeof(bool),
           typeof(StretchedGridViewItems), new PropertyMetadata(false));

        public static bool GetFillBeforeWrap(DependencyObject obj)
        {
            return (bool)obj.GetValue(FillBeforeWrapProperty);
        }

        public static void SetFillBeforeWrap(DependencyObject obj, bool value)
        {
            obj.SetValue(FillBeforeWrapProperty, value);
        }

        public static double GetMinItemWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(MinItemWidthProperty);
        }

        public static void SetMinItemWidth(DependencyObject obj, double value)
        {
            obj.SetValue(MinItemWidthProperty, value);
        }

        private static void OnMinItemWidthChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            if (s is ListViewBase f)
            {
                f.SizeChanged -= OnListViewSizeChanged;

                if (((double)e.NewValue) > 0)
                {
                    f.SizeChanged += OnListViewSizeChanged;
                }
            }
        }

        public static void ResizeItems(DependencyObject obj)
        {
            OnListViewSizeChanged(obj, null);
        }

        private static void OnListViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var itemsControl = sender as ListViewBase;

            if (itemsControl.ItemsPanelRoot is ItemsWrapGrid itemsPanel)
            {
                var total = (e?.NewSize.Width ?? itemsControl.ActualWidth) - (itemsPanel.Margin.Left + itemsPanel.Margin.Right + itemsControl.Padding.Left + itemsControl.Padding.Right);

                var itemMinSize = Math.Min(total, (double)itemsControl.GetValue(MinItemWidthProperty));

                var canBeFit = Math.Floor(total / itemMinSize);

                if ((bool)itemsControl.GetValue(FillBeforeWrapProperty) &&
                    itemsControl.Items.Count > 0 &&
                    itemsControl.Items.Count < canBeFit)
                {
                    canBeFit = itemsControl.Items.Count;
                }

                itemsPanel.ItemWidth = total / canBeFit;
            }
        }
    }
}
