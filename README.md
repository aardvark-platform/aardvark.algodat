[![Build status](https://ci.appveyor.com/api/projects/status/g9a042ab01txnf4a?svg=true)](https://ci.appveyor.com/project/stefanmaierhofer/aardvark-algodat)
[![Build status](https://travis-ci.org/aardvark-platform/aardvark.algodat.svg)](https://travis-ci.org/aardvark-platform/aardvark.algodat)
[![Join the chat at https://gitter.im/aardvark-platform/Lobby](https://img.shields.io/badge/gitter-join%20chat-blue.svg)](https://gitter.im/aardvark-platform/Lobby)
[![license](https://img.shields.io/github/license/aardvark-platform/aardvark.algodat.svg)](https://github.com/aardvark-platform/aardvark.algodat/blob/master/LICENSE)

[The Aardvark Platform](https://aardvarkians.com/) |
[Platform Wiki](https://github.com/aardvarkplatform/aardvark.docs/wiki) | 
[Gallery](https://github.com/aardvarkplatform/aardvark.docs/wiki/Gallery) | 
[Quickstart](https://github.com/aardvarkplatform/aardvark.docs/wiki/Quickstart-Windows) | 
[Status](https://github.com/aardvarkplatform/aardvark.docs/wiki/Status)

Aardvark.Algodat is part of the open-source [Aardvark platform](https://github.com/aardvark-platform/aardvark.docs/wiki) for visual computing, real-time graphics and visualization.

This repository contains high-performance, production-quality data structures and algorithms. 

* **Aardvark.Geometry.BspTree** - in-memory BSP-tree
* **Aardvark.Geometry.Clustering** - efficient clustering of geometric primitives
* **Aardvark.Geometry.Intersection** - in-memory kd-tree for polygon meshes
* **Aardvark.Geometry.PointSet** - out-of-core point cloud data management
* **Aardvark.Geometry.PointTree** - fast n-closest points queries for in-memory point clouds
* **Aardvark.Geometry.PolyMesh** - compact in-memory polygonal mesh data structure, based on [A Mesh Data Structure for Rendering and Subdivision](https://www.researchgate.net/publication/254451624_A_Mesh_Data_Structure_for_Rendering_and_Subdivision)

Furthermore, there are some importers for file formats.

* **Aardvark.Data.E57** - importer for [E57 (ASTM E2807-11)](https://www.astm.org/Standards/E2807.htm) laserscan files (compatible with `Aardvark.Geometry.PointSet`)
* **Aardvark.Data.Ascii** - fast and parameterizable importer for text-based laserscan formats, like for example *.pts* (compatible with `Aardvark.Geometry.PointSet`)
* **Aardvark.Data.Photometry** - importer of IES (IESNA LM-63) and LDT (EULUMDAT) data files; unified data strcuture for photometric measurement data, calculations, utility functions

Other:

* **Aardvark.Phsyics.Sky** - sky models: CIE Standard Genernal Sky, Hosek-Wilkie, Preetham; Astronomical calcuations for position of Sun, Moon, Planets, Stars

This software repository is made available under the terms of the [GNU Affero General Public License (AGPL)](LICENSE).

[Point Clouds Documentation](https://github.com/aardvark-platform/aardvark.docs/wiki/Point-Clouds)

[Aardvark Platform Documentation](https://github.com/aardvark-platform/aardvark.docs/wiki)
