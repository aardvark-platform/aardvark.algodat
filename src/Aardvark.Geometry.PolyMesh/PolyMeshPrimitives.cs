/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    public static class PolyMeshPrimitives
    {
        private static V2d CalculateSphericalTextureCoordinates(V3d normal)
        {
            double rad2deg = 360.0 / (2.0 * Constant.Pi);

            V3d proj2TanPlane = normal - (normal.Dot(V3d.ZAxis) * V3d.ZAxis);
            double phi = Fun.Acos(V3d.XAxis.Dot(proj2TanPlane.Normalized)) * rad2deg; //Achtung, das ist immer zwischen 0 und 180 Grad,..

            //..daher hier nochmal der check, obs nicht doch der zweite Winkel ist, der den gleichen Cos-Wert hat
            if (normal.Dot(V3d.YAxis) < 0.0)
                phi = 360.0 - phi;

            double theta = Fun.Acos(V3d.ZAxis.Dot(normal.Normalized)) * rad2deg;

            return new V2d(phi / 360.0, theta / 180.0);
        }

        #region Sphere

        /// <summary>
        /// Returns a unit sphere polymesh.
        /// vertical segments = tessellation
        /// horizontal segments = tessellation * 2
        /// </summary>
        public static PolyMesh Sphere(
                int tessellation,
                double radius,
                C4b color)
        {
            return Sphere(tessellation, radius, color, PolyMesh.Property.DiffuseColorCoordinates, Symbol.Empty);
        }

        /// <summary>
        /// Returns a unit sphere polymesh.
        /// vertical segments = tessellation
        /// horizontal segments = tessellation * 2
        /// </summary>
        public static PolyMesh Sphere(
                int tessellation,
                double radius,
                C4b color,
                Symbol coordinateName,
                Symbol tangentName)
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            var verticalSegments = tessellation;
            var horizontalSegments = tessellation * 2;

            var vertices = new List<V3d>();
            var normals = new List<V3d>();
            var indices = new List<int>();
            var firstIndexArray = new List<int>();
            var coordinates = new List<V2d>();
            var coordinateIndices = new List<int>();
            var tangentSharingIndices = new List<int>();
            var faceNormals = new List<V3d>();

            // bottom of the sphere
            V3d newVertex = -V3d.ZAxis * radius;
            vertices.Add(newVertex);
            normals.Add(-V3d.ZAxis);

            //create texture coordinates for south pole for each triangle including the pole
            for (int j = 0; j < horizontalSegments; j++)
            {
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;

                var dx = Fun.Cos(halfLongitude);
                var dy = Fun.Sin(halfLongitude);

                var halfNormal = new V3d(dx, dy, 0.0).Normalized;

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 1.0));
            }

            // create rings of vertices at progressively higher latitudes
            for (int i = 0; i < verticalSegments - 1; i++)
            {
                var latitude = ((i + 1) * Constant.Pi / verticalSegments) - Constant.PiHalf;

                var dz = Fun.Sin(latitude);
                var dxy = Fun.Cos(latitude);

                // create a single ring of vertices at this latitude
                for (int j = 0; j < horizontalSegments; j++)
                {
                    var longitude = j * Constant.PiTimesTwo / horizontalSegments;

                    var dx = Fun.Cos(longitude) * dxy;
                    var dy = Fun.Sin(longitude) * dxy;

                    var normal = new V3d(dx, dy, dz).Normalized;

                    newVertex = normal * radius;
                    vertices.Add(newVertex);
                    normals.Add(normal);

                    coordinates.Add(CalculateSphericalTextureCoordinates(normal));

                    //Anschluss-Streifen kriegt extra-coords
                    if (j + 1 == horizontalSegments)
                        coordinates.Add(new V2d(1.0, CalculateSphericalTextureCoordinates(normal).Y));
                }
            }

            // top of the sphere
            newVertex = V3d.ZAxis * radius;
            vertices.Add(newVertex);
            normals.Add(V3d.ZAxis);

            //create texture coordinates for north pole for each triangle including the pole
            for (int j = 0; j < horizontalSegments; j++)
            {
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;

                var dx = Fun.Cos(halfLongitude);
                var dy = Fun.Sin(halfLongitude);

                var halfNormal = new V3d(dx, dy, 0.0).Normalized;

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 0.0));
            }

            firstIndexArray.Add(0);
            int numInsertedVertexIndices = 0;

            // indices bottom
            for (int i = 0; i < horizontalSegments; i++)
            {
                V3d faceNormal = V3d.Zero;

                indices.Add(0);
                coordinateIndices.Add(i);
                tangentSharingIndices.Add(i);
                faceNormal += normals[indices.Last()];

                indices.Add(1 + (i + 1) % horizontalSegments);
                coordinateIndices.Add(horizontalSegments + i + 1);
                tangentSharingIndices.Add(horizontalSegments + i + 1);
                faceNormal += normals[indices.Last()];

                indices.Add(1 + i);
                coordinateIndices.Add(horizontalSegments + i);
                tangentSharingIndices.Add(horizontalSegments + i);
                faceNormal += normals[indices.Last()];

                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);

                faceNormals.Add(faceNormal.Normalized);
            }

            // indices rings
            for (int i = 0; i < verticalSegments - 2; i++)
            {
                for (int j = 0; j < horizontalSegments; j++)
                {
                    int nextI = i + 1;
                    int nextJ = j + 1;
                    int nextJModulo = nextJ % horizontalSegments;

                    V3d faceNormal = V3d.Zero;

                    indices.Add(1 + i * horizontalSegments + j);
                    coordinateIndices.Add(horizontalSegments + (horizontalSegments + 1) * i + j);
                    tangentSharingIndices.Add(horizontalSegments + (horizontalSegments + 1) * i + j);
                    faceNormal += normals[indices.Last()];

                    indices.Add(1 + i * horizontalSegments + nextJModulo);
                    coordinateIndices.Add(horizontalSegments + (horizontalSegments + 1) * i + nextJ);
                    tangentSharingIndices.Add(horizontalSegments + (horizontalSegments + 1) * i + nextJModulo);
                    faceNormal += normals[indices.Last()];

                    indices.Add(1 + nextI * horizontalSegments + nextJModulo);
                    coordinateIndices.Add(horizontalSegments + (horizontalSegments + 1) * nextI + nextJ);
                    tangentSharingIndices.Add(horizontalSegments + (horizontalSegments + 1) * nextI + nextJModulo);
                    faceNormal += normals[indices.Last()];

                    indices.Add(1 + nextI * horizontalSegments + j);
                    coordinateIndices.Add(horizontalSegments + (horizontalSegments + 1) * nextI + j);
                    tangentSharingIndices.Add(horizontalSegments + (horizontalSegments + 1) * nextI + j);
                    faceNormal += normals[indices.Last()];

                    numInsertedVertexIndices += 4;
                    firstIndexArray.Add(numInsertedVertexIndices);

                    faceNormals.Add(faceNormal.Normalized);
                }
            }

            // indices top
            for (int i = horizontalSegments; i > 0; i--)
            {
                V3d faceNormal = V3d.Zero;

                indices.Add(vertices.Count - (i + 1));
                coordinateIndices.Add(coordinates.Count - ((horizontalSegments + 1) + i));
                tangentSharingIndices.Add(coordinates.Count - ((horizontalSegments + 1) + i));
                faceNormal += normals[indices.Last()];

                if (i > 1)
                    indices.Add(vertices.Count - (i));
                else
                    indices.Add(vertices.Count - (horizontalSegments + 1));
                coordinateIndices.Add(coordinates.Count - ((horizontalSegments + 1) + (i - 1)));
                tangentSharingIndices.Add(coordinates.Count - ((horizontalSegments + 1) + (i - 1)));
                faceNormal += normals[indices.Last()];

                indices.Add(vertices.Count - 1);
                coordinateIndices.Add(coordinates.Count - i);
                tangentSharingIndices.Add(coordinates.Count - i);
                faceNormal += normals[indices.Last()];

                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);

                faceNormals.Add(faceNormal.Normalized);
            }

            var faceAttributes = new SymbolDict<Array>()
            {
                { PolyMesh.Property.Normals, faceNormals.ToArray() }
            };

            var faceVertexAttributes = tangentName == Symbol.Empty ?
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()}
            } :
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { tangentName, tangentSharingIndices.ToArray()},
            }
            ;

            var vertexAttributes = new SymbolDict<Array>()
            {
                    { PolyMesh.Property.Positions, vertices.ToArray() },
                    { PolyMesh.Property.Normals, normals.ToArray() },
                    { PolyMesh.Property.Colors, new C4b[vertices.Count].Set(color)}
            };

            var pmesh = new PolyMesh()
            {
                FaceVertexAttributes = faceVertexAttributes,
                FaceAttributes = faceAttributes,
                VertexAttributes = vertexAttributes,
                VertexIndexArray = indices.ToArray(),
                FirstIndexArray = firstIndexArray.ToArray()
            };

            return pmesh;
        }

        /// <summary>
        /// Returns a unit sphere polymesh.
        /// vertical segments = tessellation
        /// horizontal segments = tessellation * 2
        /// </summary>
        public static PolyMesh Sphere(
                int tessellation,
                double radius,
                C4b color,
                Symbol coordinateName,
                Symbol tangentName,
                Symbol binormalName
            )
        {
            var pmesh = Sphere(tessellation, radius, color, coordinateName, tangentName);

            if (binormalName.IsNotEmpty || tangentName.IsNotEmpty)
                pmesh.AddPerFaceVertexTangents(coordinateName, tangentName, binormalName, pmesh.FaceVertexAttributes[tangentName].ToArrayOfT<int>());

            return pmesh;
        }

        #endregion

        #region Cylinder

        /// <summary>
        /// Generates a cylinder with the given tessellation as segments (minimum 3). 
        /// The caps are generated as circular triangle fan from the center. 
        /// The horizontal segments are generated as quads.
        /// The origin of the cylinder is at the center and will extend from [-height/2, height/2] in Z direction.
        /// Texture coordinates can be generated optional by specifying the attribute name.
        /// The coordinates in X are wrapped around the mantle from [0, 1] in counter clockwise order starting at [+X,0].
        /// The coordinates in Y are 0 at the top and 1 at the bottom.
        /// The cap have degenerated coordinates.
        /// </summary>
        /// <param name="tessellation">Number of segments the cylinder should have</param>
        /// <param name="height">Height</param>
        /// <param name="radius">Radius</param>
        /// <param name="color">Color</param>
        /// <param name="coordinateName">Attribute name the coordinates should get. If value is Symbol.Empty no coordinates are generated.</param>
        /// <returns>PolyMesh</returns>
        public static PolyMesh Cylinder2(
            int tessellation,
            double height,
            double radius,
            C4b color, // TODO: remove / unused -> breaking change
            Symbol coordinateName
            )
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            var horizontalSegments = tessellation;

            var positions = new V3d[horizontalSegments * 2 + 2]; // two circles + 2 center of circles
            var normals = new V3d[horizontalSegments + 2]; // one circle + 2 flat faces
            var coordinates = coordinateName.IsNotEmpty ? new V2d[horizontalSegments * 3 + 4] : null; // two circles + one circle + center top/bottom
            //var tangents = new V3d[horizontalSegments + 2]; // one circles + 2 faces
            
            var topCenter = new V3d(0, 0, 0.5 * height);
            var bottomCenter = new V3d(0, 0, -0.5 * height);

            for (int j = 0; j < horizontalSegments; j++)
            {
                var circleCoord = j / (double)horizontalSegments;
                var longitude = circleCoord * Constant.PiTimesTwo;

                var n = new V3d(Fun.Cos(longitude), Fun.Sin(longitude), 0.0);

                normals[j] = n;
                //tangents[j] = new V3d(n.Y, n.Y, 0.0);
                positions[j] = topCenter + n * radius;
                positions[j + horizontalSegments] = bottomCenter + n * radius;
                if (coordinates != null)
                {
                    coordinates[j] = new V2d(circleCoord, 0);
                    coordinates[j + horizontalSegments + 1] = new V2d(circleCoord, 1);

                    coordinates[horizontalSegments * 2 + 2 + j] = new V2d(n.X, n.Y) * 0.5 + 0.5;
                }
            }

            positions[horizontalSegments * 2] = topCenter;
            positions[horizontalSegments * 2 + 1] = bottomCenter;
            normals[horizontalSegments] = V3d.ZAxis;
            normals[horizontalSegments + 1] = -V3d.ZAxis;
            //tangents[horizontalSegments] = V3d.XAxis;
            if (coordinates != null)
            {
                // coordinates for the last segment where vertex position shared, but coordinates need to be unique
                coordinates[horizontalSegments] = V2d.IO;
                coordinates[horizontalSegments * 2 + 1] = V2d.II;

                // coordinate for the top center
                coordinates[horizontalSegments * 3 + 2] = new V2d(0.5, 0.0); // top center
                coordinates[horizontalSegments * 3 + 3] = new V2d(0.5, 1.0); // bottom center
            }

            var vertexIndices = new int[horizontalSegments * (4 + 3 * 2)];
            var faceIndices = new int[horizontalSegments * 3 + 1];
            
            var quadVertexIndex = 0;
            var topTriangleVertexIndex = horizontalSegments * 4;
            var bottomTriangleVertexIndex = horizontalSegments * (4 + 3);

            for (int j = 0; j < horizontalSegments; j++)
            {
                faceIndices[j] = quadVertexIndex;

                // horizontal quad
                vertexIndices[quadVertexIndex++] = j;
                vertexIndices[quadVertexIndex++] = j + horizontalSegments;
                vertexIndices[quadVertexIndex++] = ((j + 1) % horizontalSegments) + horizontalSegments;
                vertexIndices[quadVertexIndex++] = (j + 1) % horizontalSegments;
                                
                faceIndices[horizontalSegments + j] = topTriangleVertexIndex;

                // circle segment triangle
                vertexIndices[topTriangleVertexIndex++] = j;
                vertexIndices[topTriangleVertexIndex++] = (j + 1) % horizontalSegments;
                vertexIndices[topTriangleVertexIndex++] = horizontalSegments * 2; // top center / constant

                faceIndices[horizontalSegments * 2 + j] = bottomTriangleVertexIndex;

                // circle segment triangle
                vertexIndices[bottomTriangleVertexIndex++] = horizontalSegments + ((j + 1) % horizontalSegments);
                vertexIndices[bottomTriangleVertexIndex++] = horizontalSegments + j;
                vertexIndices[bottomTriangleVertexIndex++] = horizontalSegments * 2 + 1; // bottom center / constant
            }

            faceIndices[horizontalSegments * 3] = bottomTriangleVertexIndex;

            var normalIndices = new int[horizontalSegments * (4 + 3 * 2)]; // per face vertex normals

            quadVertexIndex = 0;
            topTriangleVertexIndex = horizontalSegments * 4;
            bottomTriangleVertexIndex = horizontalSegments * (4 + 3);

            for (int j = 0; j < horizontalSegments; j++)
            {
                normalIndices[quadVertexIndex++] = j;
                normalIndices[quadVertexIndex++] = j;
                normalIndices[quadVertexIndex++] = (j + 1) % horizontalSegments;
                normalIndices[quadVertexIndex++] = (j + 1) % horizontalSegments;

                normalIndices[topTriangleVertexIndex++] = horizontalSegments;
                normalIndices[topTriangleVertexIndex++] = horizontalSegments;
                normalIndices[topTriangleVertexIndex++] = horizontalSegments;

                normalIndices[bottomTriangleVertexIndex++] = horizontalSegments + 1;
                normalIndices[bottomTriangleVertexIndex++] = horizontalSegments + 1;
                normalIndices[bottomTriangleVertexIndex++] = horizontalSegments + 1;
            }
            
            var faceVertexAttribues = new SymbolDict<Array>()
            {
                { PolyMesh.Property.Normals, normalIndices },
                { -PolyMesh.Property.Normals, normals },
            };

            if (coordinates != null)
            {
                var coordinateIndices = new int[horizontalSegments * (4 + 3 * 2)];

                quadVertexIndex = 0;
                topTriangleVertexIndex = horizontalSegments * 4;
                bottomTriangleVertexIndex = horizontalSegments * (4 + 3);

                var horizontalSegmentsPlusOne = horizontalSegments + 1;

                for (int j = 0; j < horizontalSegments; j++)
                {
                    coordinateIndices[quadVertexIndex++] = j;
                    coordinateIndices[quadVertexIndex++] = horizontalSegmentsPlusOne + j;
                    coordinateIndices[quadVertexIndex++] = horizontalSegmentsPlusOne + j + 1;
                    coordinateIndices[quadVertexIndex++] = j + 1;

                    coordinateIndices[topTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + j;
                    coordinateIndices[topTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + ((j + 1) % horizontalSegments);
                    coordinateIndices[topTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + horizontalSegments; // top center / constant

                    coordinateIndices[bottomTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + j;
                    coordinateIndices[bottomTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + ((j + 1) % horizontalSegments);
                    coordinateIndices[bottomTriangleVertexIndex++] = horizontalSegmentsPlusOne * 2 + horizontalSegments + 1; // bottom center / constant
                }

                faceVertexAttribues.Add(coordinateName, coordinateIndices);
                faceVertexAttribues.Add(-coordinateName, coordinates);
            }

            return new PolyMesh()
            {
                PositionArray = positions,
                VertexIndexArray = vertexIndices,
                FirstIndexArray = faceIndices,
                FaceVertexAttributes = new SymbolDict<Array>()
                {
                    { PolyMesh.Property.Normals, normalIndices },
                    { -PolyMesh.Property.Normals, normals },
                }
            };
        }
            
        public static PolyMesh Cylinder(
            int tessellation,
            double height,
            double radius,
            C4b color,
            Symbol coordinateName,
            Symbol tangentName
            )
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            var horizontalSegments = tessellation;

            var vertices = new List<V3d>();
            var normals = new List<V3d>();
            var indices = new List<int>();
            var firstIndexArray = new List<int>();
            var coordinates = new List<V2d>();
            var coordinateIndices = new List<int>();
            var tangentSharingIndices = new List<int>();
            var faceNormals = new List<V3d>();

            // bottom of the cylinder
            V3d newVertex = -V3d.ZAxis * (height * 0.5);
            vertices.Add(newVertex);
            
            for (int j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                var nextLongitude = ((j + 1.0) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;
                
                var normal = new V3d(Fun.Cos(longitude), Fun.Sin(longitude), 0.0).Normalized;
                var nextNormal = new V3d(Fun.Cos(nextLongitude), Fun.Sin(nextLongitude), 0.0).Normalized;
                var halfNormal = new V3d(Fun.Cos(halfLongitude), Fun.Sin(halfLongitude), 0.0).Normalized;

                newVertex = (-0.5) * height * V3d.ZAxis + normal * radius;
                vertices.Add(newVertex);

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 0));
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 1.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1));

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 1));
                normals.Add(-V3d.ZAxis);
                normals.Add(-V3d.ZAxis);
                normals.Add(-V3d.ZAxis);
            }

            // create height
            
            // create a single ring of vertices at this latitude
            for (int j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                var nextLongitude = ((j + 1) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;

                var normal = new V3d( Fun.Cos(longitude), Fun.Sin(longitude), 0).Normalized;
                var nextNormal = new V3d( Fun.Cos(nextLongitude), Fun.Sin(nextLongitude), 0).Normalized;

                normals.Add(normal);
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 1.0));

                normals.Add(nextNormal);
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 1.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1.0));
                
                normals.Add(normal);
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 0.0));
                vertices.Add(0.5 * height * V3d.ZAxis + normal * radius);

                normals.Add(nextNormal);
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 1.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1.0));
                
                normals.Add(nextNormal);
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 0.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 0.0));
                
                normals.Add(normal);
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 0.0));          
            }

            // top of the cylinder
            newVertex = V3d.ZAxis * (height * 0.5);
            vertices.Add(newVertex);

            //create texture coordinates for north pole for each triangle including the pole
            for (int j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                var nextLongitude = ((j + 1.0) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;

                var normal = new V3d(Fun.Cos(longitude), Fun.Sin(longitude), 0.0).Normalized;
                var nextNormal = new V3d(Fun.Cos(nextLongitude), Fun.Sin(nextLongitude), 0.0).Normalized;
                var halfNormal = new V3d(Fun.Cos(halfLongitude), Fun.Sin(halfLongitude), 0.0).Normalized;

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 1.0));
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 0));
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 0.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 0));
                normals.Add(V3d.ZAxis);
                normals.Add(V3d.ZAxis);
                normals.Add(V3d.ZAxis);
            }

            firstIndexArray.Add(0);
            int numInsertedVertexIndices = 0;

            int insertedBefore = 0;

            // indices bottom
            for (int i = 0; i < horizontalSegments; i++)
            {
                indices.Add(0);
                coordinateIndices.Add(i*3);
                tangentSharingIndices.Add(i*3);

                indices.Add(1 + ((i + 1) % horizontalSegments));
                coordinateIndices.Add(i*3 + 1);
                if (i + 1 < horizontalSegments)
                    tangentSharingIndices.Add(i * 3 + 1);
                else
                    tangentSharingIndices.Add(2);

                indices.Add(1 + i);
                coordinateIndices.Add(i*3 + 2);
                if(i==0)
                    tangentSharingIndices.Add(2);
                else
                    tangentSharingIndices.Add(i * 3 - 2);

                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);
            }

            insertedBefore = tangentSharingIndices.Count;

            for (int j = 0; j < horizontalSegments; j++)
            {
                int nextJ = (j+1) % horizontalSegments;

                indices.Add(1 + j);
                coordinateIndices.Add(horizontalSegments * 3 + (j*6));
                tangentSharingIndices.Add(insertedBefore + j);

                indices.Add(1 + nextJ);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 6) + 1);
                tangentSharingIndices.Add(insertedBefore + nextJ);

                indices.Add(horizontalSegments + 1 + j);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 6) + 2);
                tangentSharingIndices.Add(insertedBefore + horizontalSegments + j);

                //next triangle

                indices.Add(1 + nextJ);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 6) + 3);
                tangentSharingIndices.Add(insertedBefore + nextJ);

                indices.Add(horizontalSegments + 1 + nextJ);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 6) + 4);
                tangentSharingIndices.Add(insertedBefore + horizontalSegments + nextJ);

                indices.Add(horizontalSegments + 1 + j);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 6) + 5);
                tangentSharingIndices.Add(insertedBefore + horizontalSegments + j);

                numInsertedVertexIndices += 6;
                firstIndexArray.Add(numInsertedVertexIndices);
            }
            
            insertedBefore = tangentSharingIndices.Count;

            // indices top
            for (int i = 0; i < horizontalSegments ; i++)
            {

                //spitze:
                indices.Add(vertices.Count - 1);
                coordinateIndices.Add(horizontalSegments*3 + horizontalSegments*6 + (i*3));
                tangentSharingIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + (i * 3));

                indices.Add(1 + horizontalSegments + i);
                coordinateIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + (i * 3) + 1);
                if (i == 0)
                    tangentSharingIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + 2);
                else
                    tangentSharingIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + (i * 3) - 2);

                indices.Add(1 + horizontalSegments + ((i + 1) % horizontalSegments));
                coordinateIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + (i * 3) + 2);
                if (i + 1 < horizontalSegments)
                    tangentSharingIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + (i * 3) + 1);
                else
                    tangentSharingIndices.Add(horizontalSegments * 3 + horizontalSegments * 6 + 2);
                                
                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);
            }
            var faceVertexAttributes = tangentName == Symbol.Empty ?
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { PolyMesh.Property.Normals, normals.ToArray() }
            } :
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { tangentName, tangentSharingIndices.ToArray()},
                { PolyMesh.Property.Normals, normals.ToArray() }
            }
            ;

            var vertexAttributes = new SymbolDict<Array>()
            {
                { PolyMesh.Property.Positions, vertices.ToArray() },
                { PolyMesh.Property.Colors, new C4b[vertices.Count()].Set(color)}
            };

            var pmesh = new PolyMesh()
            {
                FaceVertexAttributes = faceVertexAttributes,
                VertexAttributes = vertexAttributes,
                VertexIndexArray = indices.ToArray(),
                FirstIndexArray = firstIndexArray.ToArray()
            };

            return pmesh;
        }

        public static PolyMesh Cylinder(
            int tessellation,
            double height,
            double radius,
            C4b color,
            Symbol coordinateName,
            Symbol tangentName,
            Symbol binormalName
            )
        {
            var pmesh = Cylinder(tessellation, height, radius, color, coordinateName, tangentName);
            
            if (binormalName.IsNotEmpty || tangentName.IsNotEmpty)
                pmesh.AddPerFaceVertexTangents(coordinateName, tangentName, binormalName, pmesh.FaceVertexAttributes[tangentName].ToArrayOfT<int>());

            return pmesh;
        }

        #endregion

        #region Cone

        /// <summary>
        /// Creates a cone with the given parameters. 
        /// The bottom circle center will be [0, 0, -height/2] and the cone top [0, 0, heigh/2].
        /// </summary>
        public static PolyMesh Cone(
        int tessellation,
        double height,
        double radius,
        C4b color
        )
        {
            return Cone(tessellation, height, radius, color, PolyMesh.Property.DiffuseColorCoordinates, Symbol.Empty);
        }

        public static PolyMesh Cone(
            int tessellation,
            double height,
            double radius,
            C4b color,
            Symbol coordinateName,
            Symbol tangentName
            )
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            var horizontalSegments = tessellation;

            var vertices = new List<V3d>();
            var normals = new List<V3d>();
            var indices = new List<int>();
            var firstIndexArray = new List<int>();
            var coordinates = new List<V2d>();
            var coordinateIndices = new List<int>();
            var tangentSharingIndices = new List<int>();
            var faceNormals = new List<V3d>();

            // bottom of the cylinder
            V3d newVertex = -V3d.ZAxis * (height * 0.5);
            vertices.Add(newVertex);

            for (int j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                var nextLongitude = ((j + 1.0) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;

                var normal = new V3d(Fun.Cos(longitude), Fun.Sin(longitude), 0.0).Normalized;
                var nextNormal = new V3d(Fun.Cos(nextLongitude), Fun.Sin(nextLongitude), 0.0).Normalized;
                var halfNormal = new V3d(Fun.Cos(halfLongitude), Fun.Sin(halfLongitude), 0.0).Normalized;

                newVertex = (-0.5) * height * V3d.ZAxis + normal * radius;
                vertices.Add(newVertex);

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 0));
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 1.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1));

                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 1));
                normals.Add(-V3d.ZAxis);
                normals.Add(-V3d.ZAxis);
                normals.Add(-V3d.ZAxis);
            }

            var x = radius / height;
            // create height
            for (int j = 0; j < horizontalSegments; j++)
            {
                var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                var nextLongitude = ((j + 1) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;
                var halfLongitude = (j + 0.5) * Constant.PiTimesTwo / horizontalSegments;

                var normal = new V3d(Fun.Cos(longitude), Fun.Sin(longitude), x).Normalized;
                var nextNormal = new V3d(Fun.Cos(nextLongitude), Fun.Sin(nextLongitude), x).Normalized;
                var halfNormal = new V3d(Fun.Cos(halfLongitude), Fun.Sin(halfLongitude), x).Normalized;

                normals.Add(normal);
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 1.0));

                normals.Add(nextNormal);
                if (j + 1 == horizontalSegments)
                    coordinates.Add(new V2d(1.0, 1.0));
                else
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1.0));

                normals.Add(halfNormal);
                coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(halfNormal).X, 0.0));               
            }

            // top of the cylinder
            newVertex = V3d.ZAxis * (height * 0.5);
            vertices.Add(newVertex);
    
            firstIndexArray.Add(0);
            int numInsertedVertexIndices = 0;

            int insertedBefore = 0;

            // indices bottom
            for (int i = 0; i < horizontalSegments; i++)
            {
                indices.Add(0);
                coordinateIndices.Add(i * 3);
                tangentSharingIndices.Add(i * 3);

                indices.Add(1 + ((i + 1) % horizontalSegments));
                coordinateIndices.Add(i * 3 + 1);
                if (i + 1 < horizontalSegments)
                    tangentSharingIndices.Add(i * 3 + 1);
                else
                    tangentSharingIndices.Add(2);

                indices.Add(1 + i);
                coordinateIndices.Add(i * 3 + 2);
                if (i == 0)
                    tangentSharingIndices.Add(2);
                else
                    tangentSharingIndices.Add(i * 3 - 2);

                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);
            }

            insertedBefore = tangentSharingIndices.Count;

            for (int j = 0; j < horizontalSegments; j++)
            {
                int nextJ = (j + 1) % horizontalSegments;

                indices.Add(1 + j);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 3));
                tangentSharingIndices.Add(insertedBefore + j);

                indices.Add(1 + nextJ);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 3) + 1);
                tangentSharingIndices.Add(insertedBefore + nextJ);

                indices.Add(vertices.Count - 1);
                coordinateIndices.Add(horizontalSegments * 3 + (j * 3) + 2);
                tangentSharingIndices.Add(insertedBefore + horizontalSegments + j);

                numInsertedVertexIndices += 3;
                firstIndexArray.Add(numInsertedVertexIndices);
            }

            insertedBefore = tangentSharingIndices.Count;

            var faceVertexAttributes = tangentName == Symbol.Empty ?
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { PolyMesh.Property.Normals, normals.ToArray() }        
            } :
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { tangentName, tangentSharingIndices.ToArray()},
                { PolyMesh.Property.Normals, normals.ToArray() }
            }
            ;

            var vertexAttributes = new SymbolDict<Array>()
            {
                    { PolyMesh.Property.Positions, vertices.ToArray() },
                    { PolyMesh.Property.Colors, new C4b[vertices.Count()].Set(color)}
            };

            var pmesh = new PolyMesh()
            {
                FaceVertexAttributes = faceVertexAttributes,
                VertexAttributes = vertexAttributes,
                VertexIndexArray = indices.ToArray(),
                FirstIndexArray = firstIndexArray.ToArray()
            };

            return pmesh;
        }

        #endregion

        #region Torus

        public static PolyMesh Torus(
            double majorRadius, double minorRadius,
            int majorTesselation, int minorTesselation,
            C4b color
            )
        {
            return Torus(majorRadius, minorRadius, majorTesselation, minorTesselation, color, PolyMesh.Property.DiffuseColorCoordinates, Symbol.Empty);
        }

        public static PolyMesh Torus(
            double majorRadius, double minorRadius,
            int majorTesselation, int minorTesselation,
            C4b color,
            Symbol coordinateName,
            Symbol tangentName
            )
        {
            var verticalSegments = majorTesselation;
            var horizontalSegments = minorTesselation;

            var coordinates = new List<V2d>();
            var coordinateIndices = new List<int>();
            var tangentSharingIndices = new List<int>();
            var faceNormals = new List<V3d>();
            var axisNormalized = V3d.YAxis;
            var trafo = Trafo3d.FromNormalFrame(V3d.OOO, V3d.YAxis);
            var firstIndexArray = new List<int>();

            var indices = new List<int>();
            var normals = new List<V3d>();
            var vertices = new List<V3d>();

            if (majorTesselation < 3 || minorTesselation < 3)
                throw new ArgumentOutOfRangeException("tesselation", "tesselation must be at least 3.");

            //calculate point array
            var majorCircle = new Circle3d(new V3d(0, 0, 0), V3d.ZAxis.Normalized, majorRadius);
            var tPoints = (0).UpTo(minorTesselation).Select(i =>
            {
                var angle = ((double)i) / minorTesselation * Constant.PiTimesTwo;
                var majorP = majorCircle.GetPoint(angle);
                var uAxis = (majorP - new V3d(0, 0, 0)).Normalized * minorRadius;
                var vAxis = V3d.ZAxis.Normalized * minorRadius;

                return (0).UpTo(majorTesselation).Select(j =>
                {
                    var angle2 = ((double)j) / majorTesselation * Constant.PiTimesTwo;
                    return majorP + uAxis * angle2.Cos() + vAxis * angle2.Sin();
                }).ToArray();
            }).ToArray();

            firstIndexArray.Add(0);
            int numInsertedVertexIndices = 0;

            //vertices, indices and coordinates
            for (int i = 1; i < tPoints.Length; i++)
            {
                var latitude = ((i + 1.0) * Constant.Pi / verticalSegments) - Constant.PiHalf;
                var dz = Fun.Sin(latitude);
                var dxy = Fun.Cos(latitude);

                for (int j = 1; j < tPoints[i-1].Length; j++)
                {
                    var longitude = j * Constant.PiTimesTwo / horizontalSegments;
                    var nextLongitude = ((j + 1.0) % horizontalSegments) * Constant.PiTimesTwo / horizontalSegments;

                    var normal = new V3d(Fun.Cos(longitude) * dxy, Fun.Sin(longitude) * dxy, dz).Normalized;
                    var nextNormal = new V3d(Fun.Cos(nextLongitude) * dxy, Fun.Sin(nextLongitude) * dxy, dz).Normalized;

                    var s = horizontalSegments * 3 + (j * 6);
                    var c = vertices.Count;
                    coordinateIndices.AddRange(new[] { s, s + 1, s + 2, s + 3, s + 4, s + 5 });
                    indices.AddRange(new[] { c + 3, c+2, c , c + 2, c + 1 , c  });

                    numInsertedVertexIndices += 6;
                    firstIndexArray.Add(numInsertedVertexIndices);

                    var quad = new[] { tPoints[i][j], tPoints[i][j - 1], tPoints[i - 1][j - 1], tPoints[i - 1][j] };
                    vertices.AddRange(quad);

                    normals.Add(normal);
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 1.0));

                    normals.Add(nextNormal);
                    if (j + 1 == horizontalSegments)
                        coordinates.Add(new V2d(1.0, CalculateSphericalTextureCoordinates(normal).Y));
                    else
                        coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1.0));

                    normals.Add(normal);
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 0.0));

                    normals.Add(nextNormal);
                    if (j + 1 == horizontalSegments)
                        coordinates.Add(new V2d(1.0, CalculateSphericalTextureCoordinates(normal).Y));
                    else
                        coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 1.0));

                    normals.Add(nextNormal);
                    if (j + 1 == horizontalSegments)
                        coordinates.Add(new V2d(0.0, CalculateSphericalTextureCoordinates(normal).Y));
                    else
                        coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(nextNormal).X, 0.0));

                    normals.Add(normal);
                    coordinates.Add(new V2d(CalculateSphericalTextureCoordinates(normal).X, 0.0));                

                    tangentSharingIndices.Add(j);
                    tangentSharingIndices.Add(j + 1);
                    tangentSharingIndices.Add(horizontalSegments + j);

                    tangentSharingIndices.Add(j + 1);
                    tangentSharingIndices.Add(horizontalSegments + j + 1);
                    tangentSharingIndices.Add(horizontalSegments + j);       
                }
            }

            var faceVertexAttributes = tangentName == Symbol.Empty ?
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()}
            }:
            new SymbolDict<Array>()
            {
                { coordinateName, coordinateIndices.ToArray()},
                { -coordinateName, coordinates.ToArray()},
                { tangentName, tangentSharingIndices.ToArray()},
            }
            ;

            var vertexAttributes = new SymbolDict<Array>()
            {
                    { PolyMesh.Property.Positions, vertices.ToArray() },
                    { PolyMesh.Property.Normals, normals.ToArray() },
                    { PolyMesh.Property.Colors, new C4b[vertices.Count()].Set(color)}
            };

            var pmesh = new PolyMesh()
            {
                FaceVertexAttributes = faceVertexAttributes,
                VertexAttributes = vertexAttributes,
                VertexIndexArray = indices.ToArray(),
                FirstIndexArray = firstIndexArray.ToArray()
            };

            return pmesh;
        }

        public static PolyMesh Torus(
            double majorRadius, double minorRadius,
            int majorTesselation, int minorTesselation,
            C4b color,
            Symbol coordinateName,
            Symbol tangentName,
            Symbol binormalName
            )
        {
            var pmesh = Torus(majorRadius, minorRadius, majorTesselation, minorTesselation, color, coordinateName, tangentName);

            if (binormalName.IsNotEmpty || tangentName.IsNotEmpty)
                pmesh.AddPerFaceVertexTangents(coordinateName, tangentName, binormalName, pmesh.FaceVertexAttributes[tangentName].ToArrayOfT<int>());

            return pmesh;
        }

        #endregion

        #region Box

        /// <summary>
        /// Returns a right-handed unit box (0,0,0)-(1,1,1)
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(C4b color)
        {
            return Box(Box3d.FromMinAndSize(V3d.OOO, V3d.III), color, true);
        }

        public static PolyMesh Box(Box3d box, C4b color) => Box(box, color, true);

        public static PolyMesh Box(Box3f box, C4b color) => Box((Box3d)box, color, true);

        /// <summary>
        /// Returns a right-handed unit box (0,0,0)-(1,1,1)
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(C4f color)
            => Box(Box3d.FromMinAndSize(V3d.OOO, V3d.III), color, true);

        public static PolyMesh Box(Box3d box, C4f color) => Box(box, color, true);

        public static PolyMesh Box(Box3f box, C4f color) => Box((Box3d)box, color, true);

        /// <summary>
        /// Returns a right-handed box
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(Box3f box, C4b color, bool createNormals)
            => Box((Box3d)box, color, createNormals);

        /// <summary>
        /// Returns a right-handed box
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(Box3d box, C4f color, bool createNormals)
        {
            var pm = Box(box, C4b.White, createNormals);
            pm.VertexAttributes[PolyMesh.Property.Colors] = new C4f[8].Set(color);

            return pm;
        }

        /// <summary>
        /// Returns a right-handed  box 
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(Box3f box, C4f color, bool createNormals)
            => Box((Box3d)box, color, createNormals);

        /// <summary>
        /// Returns a right-handed  box 
        /// with normals, tangents, binormals and texture coordinates
        /// and given color.
        /// </summary>
        public static PolyMesh Box(Box3d box, C4b color, bool createNormals)
        {
            var faceAttributes = new SymbolDict<Array>();
            var vertexAttributes = new SymbolDict<Array>();
            var faceVertexAttributes = new SymbolDict<Array>();

            // Positions and their indices

            vertexAttributes[PolyMesh.Property.Positions] = new[] 
            {
                new V3d(box.Min.X, box.Min.Y, box.Min.Z),
                new V3d(box.Max.X, box.Min.Y, box.Min.Z),
                new V3d(box.Max.X, box.Max.Y, box.Min.Z),
                new V3d(box.Min.X, box.Max.Y, box.Min.Z),
                new V3d(box.Min.X, box.Min.Y, box.Max.Z),
                new V3d(box.Max.X, box.Min.Y, box.Max.Z),
                new V3d(box.Max.X, box.Max.Y, box.Max.Z),
                new V3d(box.Min.X, box.Max.Y, box.Max.Z)
            };

            var indices = new[] 
            {
                1, 2, 6, 5, // +X
                2, 3, 7, 6, // +Y
                4, 5, 6, 7, // +Z

                3, 0, 4, 7, // -X
                0, 1, 5, 4, // -Y
                0, 3, 2, 1  // -Z
            };

            var firstIndexArray = new int[7].SetByIndex(i => i * 4);

            // Texture coordinates and their indices

            // 0,0 --- 1,0
            //  |(3) (2)| 
            //  |       | 
            //  |(0) (1)|
            // 0,1 --- 1,1

            faceVertexAttributes[PolyMesh.Property.DiffuseColorCoordinates] =
                new int[24].SetByIndex(i => i % 4);
            faceVertexAttributes[-PolyMesh.Property.DiffuseColorCoordinates] =
                new[] { V2d.OI, V2d.II, V2d.IO, V2d.OO };
            faceVertexAttributes[PolyMesh.Property.DiffuseColorUTangents] =
                new int[24].SetByIndex(i => i);

            // Normals
            if (createNormals)
            {
                faceAttributes[PolyMesh.Property.Normals] = new[]
                {
                    V3d.XAxis,  V3d.YAxis,  V3d.ZAxis,
                   -V3d.XAxis, -V3d.YAxis, -V3d.ZAxis
                };
            }

            // Colors
            vertexAttributes[PolyMesh.Property.Colors] = new C4b[8].Set(color);

            // create PolyMesh
            var pmesh = new PolyMesh()
            {
                FaceAttributes = faceAttributes,
                VertexAttributes = vertexAttributes,
                FaceVertexAttributes = faceVertexAttributes,
                VertexIndexArray = indices,
                FirstIndexArray = firstIndexArray
            };

            // Tangents and Binormals
            if (createNormals)
                pmesh.AddPerFaceVertexTangents(
                    PolyMesh.Property.DiffuseColorCoordinates,
                    PolyMesh.Property.DiffuseColorUTangents,
                    PolyMesh.Property.DiffuseColorVTangents,
                    faceVertexAttributes[PolyMesh.Property.DiffuseColorUTangents].ToArrayOfT<int>(), 24);

            return pmesh;
        }

        #endregion

        #region UnitQuad

        public static PolyMesh UnitQuad()
        {
            // OLD:
            // 0,1 --- 1,1
            //  |(3) (2)| 
            //  |       | 
            //  |(0) (1)|
            // 0,0 --- 1,0

            // NEW:
            // 0,1 --- 1,1
            //  |(2) (3)| 
            //  |       | 
            //  |(0) (1)|
            // 0,0 --- 1,0

            var g = new PolyMesh();
            var faceVertexAttributes = new SymbolDict<Array>();
            g.PositionArray = new V3d[] { V3d.OOO, V3d.IOO, V3d.OIO, V3d.IIO };
            faceVertexAttributes[PolyMesh.Property.DiffuseColorCoordinates]
                        = new V2f[] { V2f.OO, V2f.IO, V2f.OI, V2f.II };
            g.VertexIndexArray = new int[] { 0, 1, 2, 1, 3, 2 };
            g.FirstIndexArray = new int[] { 0, 3, 6 };
            return g;
        }

        #endregion

        #region Plane


        public static PolyMesh PlaneXY(V2d size)
        {
            var halfSize = size * 0.5;
            var g = new PolyMesh();
            var faceVertexAttributes = new SymbolDict<Array>();
            g.PositionArray = new V3d[] { new V3d(-halfSize.X, -halfSize.Y, 0), 
                                          new V3d(halfSize.X, -halfSize.Y, 0),  
                                          new V3d(-halfSize.X, halfSize.Y, 0),  
                                          new V3d(halfSize.X, halfSize.Y, 0) };

            g.FaceAttributes[PolyMesh.Property.Normals] = new[] { V3d.OOI };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorUTangents] = new[] { V3d.IOO };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorVTangents] = new[] { V3d.OIO };

            g.VertexAttributes[PolyMesh.Property.DiffuseColorCoordinates] = new[] { V2d.OO, V2d.IO, V2d.OI, V2d.II };

            g.FirstIndexArray = new[] { 0, 4 };
            g.VertexIndexArray = new[] { 0, 1, 3, 2 };
            
            return g;
        }

        public static PolyMesh PlaneXZ(V2d size)
        {
            var halfSize = size * 0.5;
            var g = new PolyMesh();
            var faceVertexAttributes = new SymbolDict<Array>();
            g.PositionArray = new V3d[] { new V3d(-halfSize.X, 0, -halfSize.Y), 
                                          new V3d(halfSize.X, 0, -halfSize.Y),  
                                          new V3d(-halfSize.X, 0, halfSize.Y),  
                                          new V3d(halfSize.X, 0, halfSize.Y) };

            g.FaceAttributes[PolyMesh.Property.Normals] = new[] { V3d.OIO };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorUTangents] = new[] { V3d.IOO };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorVTangents] = new[] { V3d.OOI };

			g.VertexAttributes[PolyMesh.Property.DiffuseColorCoordinates] = new[] { V2d.OO, V2d.IO, V2d.OI, V2d.II };

            g.FirstIndexArray = new[] { 0, 4 };
            g.VertexIndexArray = new[] { 0, 1, 3, 2 };
            
            return g;
        }

        public static PolyMesh PlaneYZ(V2d size)
        {
            var halfSize = size * 0.5;
            var g = new PolyMesh();

            g.PositionArray = new V3d[] { new V3d(0, -halfSize.X, -halfSize.Y), 
                                          new V3d(0, halfSize.X, -halfSize.Y),  
                                          new V3d(0, -halfSize.X, halfSize.Y),  
                                          new V3d(0, halfSize.X, halfSize.Y) };

            g.FaceAttributes[PolyMesh.Property.Normals] = new[] { V3d.IOO };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorUTangents] = new[] { V3d.OIO };
            g.FaceAttributes[PolyMesh.Property.DiffuseColorVTangents] = new[] { V3d.OOI };

            g.VertexAttributes[PolyMesh.Property.DiffuseColorCoordinates] = new[] { V2d.OO, V2d.IO, V2d.OI, V2d.II };

            g.FirstIndexArray = new[] { 0, 4 };
            g.VertexIndexArray = new[] { 0, 1, 3, 2 };
            
            return g;
        }


        #endregion

        #region Grid

        /// <summary>
        /// Builds a polymesh grid from the given heightfield using ccw-quads
        /// NOTE/TODO: no normals or texcoords
        /// </summary>
        public static PolyMesh Grid(Matrix<double> heights, V2d size)
            => Grid((V2i)heights.Size, (x, y) => new V3d(x * size.X, y * size.Y, heights[x, y]));

        public static PolyMesh Grid(V2i tesselation, V2d size)
            => Grid(tesselation, (x, y) => new V3d(x * size.X, y * size.Y, 0));

        public static PolyMesh Grid(V2i tesselation, Func<int, int, V3d> x_y_PointFunc)
        {
            var pointCount = tesselation.X * tesselation.Y;
            var primitiveCount = (tesselation.X - 1) * (tesselation.Y - 1); // quadCount

            var pa = new Matrix<V3d>(tesselation);
            var fia = new int[primitiveCount + 1];
            var via = new int[primitiveCount * 4];

            pa.SetByCoord((x, y) => x_y_PointFunc((int)x, (int)y));

            int k = 0;
            var sx = (int)tesselation.X;
            for (int y = 0; y < tesselation.Y - 1; y++)
                for (int x = 0; x < tesselation.X - 1; x++)
                {
                    // CCW
                    via[k++] = x + y * sx;
                    via[k++] = x + 1 + y * sx;
                    via[k++] = x + 1 + (y + 1) * sx;
                    via[k++] = x + (y + 1) * sx;
                }

            fia.SetByIndex(j => j * 4);

            var g = new PolyMesh()
            {
                PositionArray = pa.Data,
                VertexIndexArray = via,
                FirstIndexArray = fia,
            };

            return g;
        }

        #endregion

        #region Circle

        /// <summary>
        /// Generates a circle primitive as single face with the specified number of segments. 
        /// The circle is aligned in the XY-plane and centroid at [0, 0, 0].
        /// The winding order is counter-clockwise starting at [X, 0] and
        /// a face normal pointing to [0, 0, 1] direction.
        /// </summary>
        /// <param name="tessellation">Number of segments</param>
        /// <param name="radius">Radius</param>
        /// <returns>PolyMesh</returns>
        public static PolyMesh Circle(
            int tessellation,
            double radius
            )
        {
            if (tessellation < 3)
                throw new ArgumentOutOfRangeException("tessellation");

            var positions = new V3d[tessellation].SetByIndex(i =>
            {
                var circleCoord = i / (double)tessellation;
                var longitude = circleCoord * Constant.PiTimesTwo;

                return radius * new V3d(Fun.Cos(longitude), Fun.Sin(longitude), 0.0);
            });

            // single face with tessellation + 1 vertices
            var vertexIndices = new int[tessellation].SetByIndex(i => i);
            var faceIndices = new int[2].SetByIndex(i => i * tessellation);
            
            return new PolyMesh()
            {
                PositionArray = positions,
                VertexIndexArray = vertexIndices,
                FirstIndexArray = faceIndices,
                FaceAttributes = new SymbolDict<Array>()
                {
                    { PolyMesh.Property.Normals, V3d.OOI.IntoArray() },
                }
            };
        }

        #endregion

        #region Misc Helper

        private static V3d GetCirclePos(double i, int tessellation, Trafo3d trafo)
        {
            var angle = i * Constant.PiTimesTwo / tessellation;

            var dx = Fun.Cos(angle);
            var dy = Fun.Sin(angle);

            var v = new V3d(dx, dy, 0);
            var tv = trafo.Forward.TransformPos(v);

            return tv;
        }

        #endregion
    }
}

