using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Geometry.Points.Views
{
    /// <summary>
    /// </summary>
    public class FilteredPointCloudNode : IPointCloudNode
    {
        /// <summary>
        /// </summary>
        public FilteredPointCloudNode(
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
        {

        }
    }
}
