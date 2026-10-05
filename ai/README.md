# Aardvark.Algodat Reference

Read only the guide needed for your task. Build, test, dependency, and editing rules are in [AGENTS.md](../AGENTS.md).

| Task | Guide and entry points |
|------|------------------------|
| Point cloud storage and queries | [Point clouds](POINT_CLOUDS.md): `PointSet`, `PointSetNode`, `Chunk`, `GenericChunk`, `Storage`, `PersistentRef`, `PointRkdTreeF` |
| Geometry algorithms and coordinate transformations | [Geometry](GEOMETRY.md): `BspTreeBuilder`, `PointEpsilonClustering`, `KdIntersectionTree`, `Normals`, `CoordinateSystem` |
| Polygon meshes | [PolyMesh](POLYMESH.md): construction, attributes, topology, operations |
| Point cloud file formats | [Importers](IMPORTERS.md): `PointCloud.Import`, `E57`, `Laszip`, `Ply`, `Ascii`, `Pts`, `Yxh` |
| Sky models and astronomy | [Sky](SKY.md): `CIESky`, `HosekSky`, `PreethamSky`, `SunPosition`, `MoonPosition`, `Astronomy` |
| Point cloud rendering | [Rendering](RENDERING.md): `LodTreeInstance`, `PointSetRenderConfig`, `SSAOConfig`, picking |
