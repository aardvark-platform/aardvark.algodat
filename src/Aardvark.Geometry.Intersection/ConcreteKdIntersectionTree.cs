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
using Aardvark.Base.Coder;
using System.Collections.Generic;

namespace Aardvark.Geometry
{
    /// <summary>
    /// A concrete kd intersection tree contains a kd tree and
    /// a trafo.
    /// </summary>
    [RegisterTypeInfo]
    public class ConcreteKdIntersectionTree : IFieldCodeable
    {
        public KdIntersectionTree KdIntersectionTree;
        public Trafo3d Trafo;
        public SymbolDict<object> Attributes;

        public ConcreteKdIntersectionTree()
        {
            KdIntersectionTree = new KdIntersectionTree();
            Trafo = Trafo3d.Identity;
        }

        public ConcreteKdIntersectionTree(
                    KdIntersectionTree kdIntersectionTree,
                    Trafo3d trafo)
        {
            KdIntersectionTree = kdIntersectionTree;
            Trafo = trafo;
        }

        public static explicit operator ConcreteKdIntersectionTree(KdIntersectionTree kdTree)
        {
            return new ConcreteKdIntersectionTree(kdTree, Trafo3d.Identity);
        }

        #region IFieldCodeable Members

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "KdIntersectionTree", (c,o) => c.CodeT(ref ((ConcreteKdIntersectionTree)o).KdIntersectionTree) ),
                new FieldCoder(1, "Trafo", (c,o) => c.CodeTrafo3d(ref ((ConcreteKdIntersectionTree)o).Trafo) ),
            };
        }

        #endregion
    }

    public static class ConcreteKdIntersectionTreeExtensions
    {
        /// <summary>
        /// Returns a concrete kd intersection tree using the provided
        /// kd tree and an identity trafo.
        /// </summary>
        public static ConcreteKdIntersectionTree ToConcreteKdIntersectionTree(
            this KdIntersectionTree kdTree)
        {
            return new ConcreteKdIntersectionTree(kdTree, Trafo3d.Identity);
        }
    }
}
