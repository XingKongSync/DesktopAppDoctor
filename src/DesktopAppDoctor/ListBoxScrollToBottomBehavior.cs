using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopAppDoctor
{
    public class ListBoxScrollToBottomBehavior : Behavior<ListBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            ((ICollectionView)AssociatedObject.Items).CollectionChanged += ListBoxScrollToBottomBehavior_CollectionChanged;
        }
        private void ListBoxScrollToBottomBehavior_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ////这里正好使用了ListBox的ScrollIntoView()方法，也只有ListBox类型才有这个方法
            ////其父类没有这里方法，所以T使用的ListBox,没有使用其父类
            //if (AssociatedObject.HasItems)
            //    AssociatedObject.ScrollIntoView(AssociatedObject.Items[AssociatedObject.Items.Count - 1]);
            try
            {
                if (AssociatedObject.IsLoaded)
                {
                    Decorator decorator = (Decorator)VisualTreeHelper.GetChild(AssociatedObject, 0);
                    ScrollViewer scrollViewer = (ScrollViewer)decorator.Child;
                    scrollViewer.ScrollToEnd();
                }
            }
            catch (Exception)
            {
            }

        }
        protected override void OnDetaching()
        {
            base.OnDetaching();
            ((ICollectionView)AssociatedObject.Items).CollectionChanged -= ListBoxScrollToBottomBehavior_CollectionChanged;
        }
    }
}
