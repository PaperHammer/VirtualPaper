using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace VirtualPaper.UIComponent.Data {
    public partial class AutoSizeWrap : Panel {
        protected override Size MeasureOverride(Size availableSize) {
            var desiredSize = new Size();
            double rowHeight = 0;
            double currentWidth = 0;

            foreach (UIElement child in Children) {
                child.Measure(availableSize);
                Size childDesiredSize = child.DesiredSize;

                if (currentWidth + childDesiredSize.Width > availableSize.Width && currentWidth > 0) {
                    // Move to next line
                    desiredSize.Width = Math.Max(desiredSize.Width, currentWidth);
                    desiredSize.Height += rowHeight;
                    currentWidth = 0;
                    rowHeight = 0;
                }

                currentWidth += childDesiredSize.Width;
                rowHeight = Math.Max(rowHeight, childDesiredSize.Height);
            }

            desiredSize.Width = Math.Max(desiredSize.Width, currentWidth);
            desiredSize.Height += rowHeight;

            return desiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            double rowHeight = 0;
            double currentWidth = 0;
            double yPosition = 0;

            foreach (UIElement child in Children) {
                Size childDesiredSize = child.DesiredSize;

                if (currentWidth + childDesiredSize.Width > finalSize.Width && currentWidth > 0) {
                    // Move to next line
                    yPosition += rowHeight;
                    currentWidth = 0;
                    rowHeight = 0;
                }

                Rect rect = new(currentWidth, yPosition, childDesiredSize.Width, childDesiredSize.Height);
                child.Arrange(rect);

                currentWidth += childDesiredSize.Width;
                rowHeight = Math.Max(rowHeight, childDesiredSize.Height);
            }

            return finalSize;
        }
    }
}
