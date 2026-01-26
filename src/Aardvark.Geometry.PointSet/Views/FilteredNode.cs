/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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

using Aardvark.Base;
using Aardvark.Base.Sorting;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using Aardvark.Base.IL;
using static Aardvark.Data.Durable;

namespace Aardvark.Geometry.Points;


/// <summary>
/// A filtered view onto a point cloud.
/// </summary>
public class FilteredNode : IPointNode
{
    /// <summary>
    /// Creates a FilteredNode.
    /// </summary>
    public static IPointNode Create(string id, IPointNode node, IFilter filter)
    {
        return new FilteredNode(id, node, filter);
    }
    
    public static IPointNode Create(IPointNode node, IFilter filter)
        => Create(node.Id, node, filter);

    private string m_id;
    /// <summary>
    /// </summary>
    private FilteredNode(string id, IPointNode node, IFilter filter)
    {
        m_id = id;
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));

        if (filter.IsFullyInside(node)) m_activePoints = null;
        else if (filter.IsFullyOutside(node)) m_activePoints = [];
        else m_activePoints = Filter.FilterPoints(node, m_activePoints);
    }

    private PersistentRef<IPointNode>?[]? m_subnodes_cache;

    private readonly HashSet<int>? m_activePoints;

    /// <summary></summary>
    public string Id => m_id.ToString();

    /// <summary> </summary>
    public IPointNode Node { get; }

    /// <summary></summary>
    public IFilter Filter { get; }

    private Lazy<int[]> filteredIndices => new (() => Filter.FilterPoints(Node).ToArray());
    
    /// <summary></summary>
    public bool IsEmpty => Node.Positions.Length == 0 && Node.Children.Length == 0;

    public Box3d CellBounds => Node.CellBounds;
    public Box3d DataBounds => Node.DataBounds;
    public V3d[] Positions => filteredIndices.Value.Map((i) => Node.Positions[i]);

    public PointKdTree? KdTree {
        get
        {
            var posf = Positions.Map(p => (p - Positions[0]).ToV3f());
            return new PointKdTree(posf.BuildKdTree(), Positions[0]); 
        }
    }

    public bool TryGetAttribute(Symbol name, out Array data)
    {
        if (Node.TryGetAttribute(name, out var innerDate))
        {
            var res = innerDate.Subset(filteredIndices.Value);
            data = res;
            return true;
        }

        data = null;
        return false;
        
    }

    /// <summary></summary>
    // public IPointNode[] Children
    // {
    //     get
    //     {
    //         if (Node.Children == null) return null;
    //
    //         if (m_subnodes_cache == null)
    //         {
    //             m_subnodes_cache = new PersistentRef<IPointNode>[8];
    //             for (var i = 0; i < 8; i++)
    //             {
    //                 var subCell = Cell.GetOctant(i);
    //
    //                 var spatial = Filter as ISpatialFilter;
    //
    //                 if (spatial != null && spatial.IsFullyInside(subCell.BoundingBox))
    //                 {
    //                     m_subnodes_cache[i] = Node.Subnodes[i];
    //                 }
    //                 else if (spatial != null && spatial.IsFullyOutside(subCell.BoundingBox))
    //                 {
    //                     m_subnodes_cache[i] = null;
    //                 }
    //                 else
    //                 {
    //                     var id = (Id + "." + i).ToGuid();
    //                     var n0 = Node.Subnodes[i]?.Value;
    //                     if (n0 != null)
    //                     {
    //                         if (Filter.IsFullyInside(n0))
    //                         {
    //                             m_subnodes_cache[i] = Node.Subnodes[i];
    //                         }
    //                         else if (Filter.IsFullyOutside(n0))
    //                         {
    //                             m_subnodes_cache[i] = null;
    //                         }
    //                         else if (n0 != null)
    //                         {
    //                             var n = new FilteredNode(id, false, n0, Filter);
    //                             m_subnodes_cache[i] = new PersistentRef<IPointNode>(id, n);
    //                         }
    //                     }
    //                     else
    //                     {
    //                         m_subnodes_cache[i] = null;
    //                     }
    //                 }
    //
    //
    //             }
    //         }
    //         return m_subnodes_cache!;
    //     }
    // }
    public IPointNode[] Children {
        get
        {
            
            var spatial = Filter as ISpatialFilter;
            if (spatial != null)
            {
                var result = new List<IPointNode>(Node.Children.Length);
                foreach (var child in Node.Children)
                {
                    if (spatial.IsFullyInside(child.CellBounds))
                    {
                        result.Add(child);
                    }
                    else if (!spatial.IsFullyOutside(child.CellBounds))
                    {
                        result.Add(Create(child.Id, child, Filter));
                    }
                }

                return result.ToArray();
            }
            else {
                return Node.Children.Map(c => FilteredNode.Create(c.Id, c, Filter));
            }

        }
    }
}
