namespace Aardvark.Data.Quadtree

open Aardvark.Data
open System

[<AutoOpen>]
module Defs =

    let private def id name description (typ : Durable.Def) = Durable.Def(Guid.Parse(id), name, description, typ.Id, false)

    module Quadtree =
        let Node                    = def "e497f9c1-c903-41c4-91de-32bf76e009da" "Quadtree.Node" "A quadtree node. DurableMapAligned16." Durable.Primitives.DurableMapAligned16
        let NodeId                  = def "e46c4163-dd28-43a4-8254-bc21dc3f766b" "Quadtree.NodeId" "Quadtree. Unique id of a node. Guid." Durable.Primitives.GuidDef
        let CellBounds              = def "59258849-5765-4d11-b760-538282063a55" "Quadtree.CellBounds" "Quadtree. Node bounds in cell space. Cell2d." Durable.Aardvark.Cell2d
        let SampleMapping           = def "2f363f2a-2e52-4a86-a620-d8689db511ad" "Quadtree.SampleMapping" "Quadtree. Mapping of sample values to cell space. Box2l." Durable.Aardvark.Box2l
        let SampleSizePotExp        = def "1aa56aca-de4c-4705-9baf-11f8766a0892" "Quadtree.SampleSizePotExp" "Quadtree. Size of a sample is 2^SampleSizePotExp. Int32." Durable.Primitives.Int32
        let SubnodeIds              = def "a2841629-e4e2-4b90-bdd1-7a1a5a41bded" "Quadtree.SubnodeIds" "Quadtree. Subnodes as array of guids. Array length is 4 for inner nodes (where Guid.Empty means no subnode) and no array for leaf nodes. Guid[]." Durable.Primitives.GuidArray
        
        let Heights1f               = def "4cb689c5-b627-4bcd-9db7-5dbd24d7545a" "Quadtree.Heights1f" "Quadtree. Height value per sample. Float32[]." Durable.Primitives.Float32Array
        let Heights1fRef            = def "fcf042b4-fe33-4e28-9aea-f5526600f8a4" "Quadtree.Heights1f.Reference" "Quadtree. Reference to Quadtree.Heights1f. Guid." Durable.Primitives.GuidDef

        let Heights1d               = def "c66a4240-00ef-44f9-b377-0667f279b97e" "Quadtree.Heights1d" "Quadtree. Height value per sample. Float64[]." Durable.Primitives.Float64Array
        let Heights1dRef            = def "baa8ed40-57e3-4f88-8d11-0b547494c8cb" "Quadtree.Heights1d.Reference" "Quadtree. Reference to Quadtree.Heights1d. Guid." Durable.Primitives.GuidDef
        
        let Normals3f               = def "d5166ae4-7bea-4ebe-a3bf-cae8072f8951" "Quadtree.Normals3f" "Quadtree. Normal vector per sample. V3f[]." Durable.Aardvark.V3fArray
        let Normals3fRef            = def "817ecbb6-1e86-41a2-b1ee-53884a27ea97" "Quadtree.Normals3f.Reference" "Quadtree. Reference to Quadtree.Normals3f. Guid." Durable.Primitives.GuidDef
        
        let Heights1dWithOffset     = def "924ae8a2-7b9b-4e4d-a609-7b0381858499" "Quadtree.Heights1dWithOffset" "Quadtree. Height value per sample. Float64 offset + Float32[] values." Durable.Primitives.Float32ArrayWithFloat64Offset
        let Heights1dWithOffsetRef  = def "2815c5a7-48bf-48b6-ba7d-5f4e98f6bc47" "Quadtree.Heights1dWithOffset.Reference" "Quadtree. Reference to Quadtree.Heights1dWithOffset. Guid." Durable.Primitives.GuidDef

        let HeightStdDevs1f         = def "74bfe324-98ad-4f57-8163-120361e1e68e" "Quadtree.HeightStdDevs1f" "Quadtree. Standard deviation per height value. Float32[]." Durable.Primitives.Float32Array
        let HeightStdDevs1fRef      = def "f93e8c5f-7e9e-4e1f-b57a-4475ebf023af" "Quadtree.HeightStdDevs1f.Reference" "Quadtree. Reference to Quadtree.HeightStdDevs1f. Guid." Durable.Primitives.GuidDef

        let Colors3b                = def "378d93ae-45e2-4e6a-9018-f09c88e7d10f" "Quadtree.Colors3b" "Quadtree. Color value per sample. C3b[]." Durable.Aardvark.C4bArray
        let Colors3bRef             = def "d6f8750c-6e94-4c9f-9f83-099268e95cc5" "Quadtree.Colors3b.Reference" "Quadtree. Reference to Quadtree.Colors3b. Guid." Durable.Primitives.GuidDef

        let Colors4b                = def "97b8282c-964a-40e8-a7be-d55ee587b5d4" "Quadtree.Colors4b" "Quadtree. Color value per sample. C4b[]." Durable.Aardvark.C4bArray
        let Colors4bRef             = def "8fe18316-7fa3-4704-869d-c3995d19d03e" "Quadtree.Colors4b.Reference" "Quadtree. Reference to Quadtree.Colors4b. Guid." Durable.Primitives.GuidDef

        let Intensities1i           = def "da564b5d-c5a4-4274-806a-acd04fa206b2" "Quadtree.Intensities1i" "Quadtree. Intensity value per sample. Int32[]." Durable.Primitives.Int32Array
        let Intensities1iRef        = def "b44484ba-e9a6-4e0a-a26a-3641a91ee9cf" "Quadtree.Intensities1i.Reference" "Quadtree. Reference to Quadtree.Intensities1i. Guid." Durable.Primitives.GuidDef
