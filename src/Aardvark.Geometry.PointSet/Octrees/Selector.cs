/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary></summary>
    public class Selector
    {
        /// <summary>
        /// Everything is selected.
        /// </summary>
        public static readonly Selector All = new Selector(Selection.All);

        /// <summary>
        /// Nothing is selected.
        /// </summary>
        public static readonly Selector None = new Selector(Selection.None);

        /// <summary></summary>
        public static Selector Custom(
            Func<PointSetNode, bool> isFullyInsideSelection,
            Func<PointSetNode, bool> isFullyOutsideSelection,
            Func<PointSetNode, IList<int>> getSelectedPointIndices
            )
            => new Selector(n =>
            {
                if (isFullyInsideSelection(n)) return Selection.All(n);
                if (isFullyOutsideSelection(n)) return Selection.None(n);
                var ia = getSelectedPointIndices(n);
                return (ia == null || ia.Count == 0) ? Selection.None(n) : Selection.Partial(n, ia);
            });

        /// <summary></summary>
        public static Selector GlobalPositions(
            Func<PointSetNode, bool> isFullyInsideSelection,
            Func<PointSetNode, bool> isFullyOutsideSelection,
            Func<V3d, bool> isGlobalPositionSelected
            )
            => Custom(
                isFullyInsideSelection,
                isFullyOutsideSelection,
                n =>
            {
                if (!n.HasPositions) return null;
                var xs = n.PositionsAbsolute;
                var ia = new List<int>();
                for (var i = 0; i < xs.Length; i++) if (isGlobalPositionSelected(xs[i])) ia.Add(i);
                return ia;
            });

        /// <summary></summary>
        public static Selector LocalPositions(
            Func<PointSetNode, bool> isFullyInsideSelection,
            Func<PointSetNode, bool> isFullyOutsideSelection,
            Func<V3f, bool> isLocalPositionSelected
            )
            => Custom(
                isFullyInsideSelection,
                isFullyOutsideSelection,
                n =>
                {
                    if (!n.HasPositions) return null;
                    var xs = n.Positions.Value;
                    var ia = new List<int>();
                    for (var i = 0; i < xs.Length; i++) if (isLocalPositionSelected(xs[i])) ia.Add(i);
                    return ia;
                });

        /// <summary></summary>
        public static Selector Colors(
            Func<PointSetNode, bool> isFullyInsideSelection,
            Func<PointSetNode, bool> isFullyOutsideSelection,
            Func<C4b, bool> isColorSelected
            )
            => Custom(
                isFullyInsideSelection,
                isFullyOutsideSelection,
                n =>
                {
                    if (!n.HasColors) return null;
                    var xs = n.Colors.Value;
                    var ia = new List<int>();
                    for (var i = 0; i < xs.Length; i++) if (isColorSelected(xs[i])) ia.Add(i);
                    return ia;
                });
        
        /// <summary></summary>
        public struct Selection
        {
            /// <summary></summary>
            public readonly PointSetNode Node;
            /// <summary></summary>
            public readonly IList<int> SelectedPointIndices;
            /// <summary></summary>
            public readonly bool IsNodeFullyInsideSelection;
            /// <summary></summary>
            public readonly bool IsNodeFullyOutsideSelection;
            /// <summary></summary>
            public bool IsNodePartiallySelected => SelectedPointIndices != null;

            /// <summary></summary>
            public Selection(PointSetNode node, IList<int> selectedPointIndices, bool isNodeFullyInsideSelection, bool isNodeFullyOutsideSelection)
            {
                Node = node ?? throw new NullReferenceException(nameof(node));
                if (selectedPointIndices != null)
                    if (isNodeFullyInsideSelection || isNodeFullyOutsideSelection) throw new ArgumentException();
                else
                    if (!(isNodeFullyInsideSelection ^ isNodeFullyOutsideSelection)) throw new ArgumentException();

                SelectedPointIndices = selectedPointIndices;
                IsNodeFullyInsideSelection = isNodeFullyInsideSelection;
                IsNodeFullyOutsideSelection = isNodeFullyOutsideSelection;
            }

            /// <summary></summary>
            public static Selection All(PointSetNode node) => new Selection(node, null, true, false);
            /// <summary></summary>
            public static Selection None(PointSetNode node) => new Selection(node, null, false, true);
            /// <summary></summary>
            public static Selection Partial(PointSetNode node, IList<int> selectedPointIndices) => new Selection(node, selectedPointIndices, false, false);
        }

        /// <summary></summary>
        public readonly Func<PointSetNode, Selection> Select;

        /// <summary></summary>
        public Selector(Func<PointSetNode, Selection> select)
        {
            Select = select ?? throw new ArgumentNullException(nameof(select));
        }
    }
};
