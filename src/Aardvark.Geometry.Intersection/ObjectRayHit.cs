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
using System.Collections.Generic;

namespace Aardvark.Geometry
{

    /// <summary>
    /// An IntersectionRayHit contains the immediate information about an
    /// intersection of a ray with an object.
    /// </summary>
    public struct ObjectRayHit
    {
        public RayHit3d RayHit;
        public SetObject SetObject;
        public List<SetObject> ObjectStack;
        public object Tag;

        #region Constructor

        /// <summary>
        /// Create an intersection ray hit and initializes it so that
        /// that intersections with a t smaller than a supplied maximal t
        /// parameter can be found. The IntersectionRayHit contains a
        /// stack of IntersectionObjects for hierarchical use of
        /// <see cref="KdIntersectionTree"/>s.
        /// In a flat configuration this stack is not used.
        /// </summary>
        /// <param name="maxT">The maximal possible t parameter on the ray.</param>
        public ObjectRayHit(double maxT)
        {
            RayHit = new RayHit3d(maxT);
            SetObject.Index = -1;
            SetObject.Set = null;
            ObjectStack = null;
            Tag = null;
        }

        #endregion

        #region Set Method

        public bool Set(IIntersectableObjectSet set, int index)
        {
            SetObject.Set = set;
            SetObject.Index = index;
            return true;
        }

        public bool Set(IIntersectableObjectSet set, int index, int part)
        {
            RayHit.Part = part;
            SetObject.Set = set;
            SetObject.Index = index;
            return true;
        }

        #endregion

        #region Static Constants

        public readonly static ObjectRayHit MaxRange = new(double.PositiveInfinity);

        #endregion

        #region RayHitInfo

        /// <summary>
        /// Create an addtional data structure that contains information
        /// about the hit that is not already part of the IntersectionRayHit.
        /// </summary>
        public ObjectHitInfo GetIntersectionRayHitInfo()
        {
            ObjectHitInfo hitInfo = new();
            SetObject.Set.ObjectHitInfo(this, ref hitInfo);
            return hitInfo;
        }

        #endregion
    }

    /// <summary>
    /// An object that is intersected by a ray is uniquely identified by
    /// the <see cref="IIntersectableObjectSet"/> it belongs to, and its
    /// index within this set.
    /// </summary>
    public struct SetObject
    {
        public int Index;
        public IIntersectableObjectSet Set;

        #region Constructor

        public SetObject(IIntersectableObjectSet set, int index)
        {
            Set = set; Index = index;
        }

        #endregion

        #region Static Constants

        public static readonly SetObject Invalid = new(null, -1);

        #endregion
    }

    /// <summary>
    /// An ObjectRayHitInfo contains additional intersection parameters
    /// that can be computed after an actual intersection has been found.
    /// </summary>
    public struct ObjectHitInfo
    {
        public V3d Normal;      // Normal vector at hitpoint.
        public V3d[] Points;    // vertices of object
        public V3d[] Edges;     // edges of object

        #region Properties

        public bool HasValidNormal
        {
            get { return Normal != V3d.Zero; }
        }
        
        #endregion
    }

    /// <summary>
    /// This structure contains the result of a kd-Tree closest point query.
    /// </summary>
    public struct ObjectClosestPoint
    {
        public double DistanceSquared;
        public double Distance;
        public V3d Point;
        public V2d Coord;
        public SetObject SetObject;
        public List<SetObject> ObjectStack;

        #region Set Method

        public bool Set(
                double distanceSquared,
                V3d point,
                IIntersectableObjectSet set,
                int index)
        {
            DistanceSquared = distanceSquared;
            Distance = distanceSquared.Sqrt();
            Point = point;
            SetObject = new SetObject(set, index);
            return true;
        }

        #endregion

        #region Static Constants

        public static readonly ObjectClosestPoint MaxRange = new()
        {
            DistanceSquared = double.MaxValue,
            Distance = double.MaxValue,
            Point = V3d.NaN,
            SetObject = SetObject.Invalid,
            ObjectStack = null
        };

        #endregion
    }
}
