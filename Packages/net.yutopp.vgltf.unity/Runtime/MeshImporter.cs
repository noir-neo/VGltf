﻿//
// Copyright (c) 2019- yutopp (yutopp@gmail.com)
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at  https://www.boost.org/LICENSE_1_0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VGltf.Unity
{
    public abstract class MeshImporterHook
    {
        public virtual void PostHook(MeshImporter importer)
        {
        }
    }

    public class MeshImporter : ImporterRefHookable<MeshImporterHook>
    {
        public override IImporterContext Context { get; }

        public MeshImporter(IImporterContext context)
        {
            Context = context;
        }

        public async Task<IndexedResource<Mesh>> Import(int meshIndex, GameObject go, CancellationToken ct)
        {
            var gltf = Context.Container.Gltf;
            var gltfMesh = gltf.Meshes[meshIndex];

            return await Context.Resources.Meshes.GetOrCallAsync(meshIndex, async () =>
            {
                return await ForceImport(meshIndex, go, ct);
            });
        }

        sealed class Target : IEquatable<Target>
        {
            public int Position;
            public int? Normal;
            public int? Tangent;

            public override bool Equals(object obj)
            {
                return Equals(obj as Target);
            }

            public bool Equals(Target other)
            {
                return other != null &&
                       Position == other.Position &&
                       EqualityComparer<int?>.Default.Equals(Normal, other.Normal) &&
                       EqualityComparer<int?>.Default.Equals(Tangent, other.Tangent);
            }

            public override int GetHashCode()
            {
                throw new NotImplementedException();
            }
        }

        sealed class Primitive
        {
            public int? Indices;
            public int? Material;
            public int? Position;
            public int? Normal;
            public int? Tangent;
            public int? TexCoord0;
            public int? TexCoord1;
            public int? Color;
            public int? Weight;
            public int? Joint;
            public List<Target> Targets;
        }

        sealed class PrimitiveResource
        {
            public int[] Indices;
            public Material Material;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] UV;
            public Vector2[] UV2;
            public Color[] Colors;
            public BoneWeight[] BoneWeights;
            public BlendShapeResource[] BlendShapes;
        }

        sealed class BlendShapeResource
        {
            public string Name;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector3[] Tangents;
        }

        public async Task<IndexedResource<Mesh>> ForceImport(int meshIndex, GameObject go, CancellationToken ct)
        {
            var gltf = Context.Container.Gltf;
            var gltfMesh = gltf.Meshes[meshIndex];

            var primsRaw = gltfMesh.Primitives
                .Select(p => ExtractPrimitive(p))
                .ToArray();

            ;
            foreach (var (p, i) in primsRaw.Skip(1).Select((p, i) => (p, i)))
            {
                ValidateSubPrimitives(primsRaw[0], p, i);
            }

            var prims = await Task.WhenAll(primsRaw.Select((p, i) => ImportPrimitive(gltfMesh, p, i == 0, ct)));

            var mesh = new Mesh();
            mesh.name = gltfMesh.Name;

            var resource = Context.Resources.Meshes.Add(meshIndex, meshIndex, mesh.name, mesh);

            mesh.subMeshCount = gltfMesh.Primitives.Count;

            var materials = new List<Material>();
            var skinedMesh = false;
            var submeshIndex = 0;
            foreach (var prim in prims)
            {
                if (submeshIndex == 0)
                {
                    skinedMesh = prim.BoneWeights != null;

                    mesh.vertices = prim.Vertices;
                    mesh.normals = prim.Normals;
                    mesh.tangents = prim.Tangents;
                    mesh.uv = prim.UV;
                    mesh.uv2 = prim.UV2;
                    mesh.colors = prim.Colors;
                    mesh.boneWeights = prim.BoneWeights;
                    if (prim.BlendShapes != null)
                    {
                        foreach (var b in prim.BlendShapes)
                        {
                            mesh.AddBlendShapeFrame(b.Name, 100.0f, b.Vertices, b.Normals, b.Tangents);
                        }
                    }
                }

                mesh.SetIndices(prim.Indices, MeshTopology.Triangles, submeshIndex);
                submeshIndex++;

                materials.Add(prim.Material);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            Renderer r = null;
            if (skinedMesh)
            {
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.enabled = false; // Do not render by default until explicitly enabled

                // Default blend shape weight
                if (gltfMesh.Weights != null)
                {
                    foreach (var (w, i) in gltfMesh.Weights.Select((w, i) => (w, i)))
                    {
                        // gltf[0, 1] -> Unity[0, 100]
                        smr.SetBlendShapeWeight(i, w * 100.0f);
                    }
                }

                r = smr;
            }
            else
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.enabled = false; // Do not render by default until explicitly enabled
                r = mr;
            }

            r.sharedMaterials = materials.ToArray();

            return resource;
        }

        Primitive ExtractPrimitive(Types.Mesh.PrimitiveType gltfPrim)
        {
            var gltfAttr = gltfPrim.Attributes;

            var res = new Primitive();

            res.Indices = gltfPrim.Indices;
            res.Material = gltfPrim.Material;

            {
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.POSITION, out var index))
                {
                    res.Position = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.NORMAL, out index))
                {
                    res.Normal = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.TANGENT, out index))
                {
                    res.Tangent = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.TEXCOORD_0, out index))
                {
                    res.TexCoord0 = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.TEXCOORD_1, out index))
                {
                    res.TexCoord1 = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.COLOR_0, out index))
                {
                    res.Color = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.WEIGHTS_0, out index))
                {
                    res.Weight = index;
                }
            }

            {
                int index;
                if (gltfAttr.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.JOINTS_0, out index))
                {
                    res.Joint = index;
                }
            }

            if (res.Weight != null || res.Joint != null)
            {
                if (res.Weight == null || res.Joint == null)
                {
                    throw new NotImplementedException(""); // TODO: fix
                }
            }

            if (gltfPrim.Targets != null)
            {
                var targets = new List<Target>();
                foreach (var t in gltfPrim.Targets)
                {
                    var target = new Target();
                    {
                        int index;
                        if (!t.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.POSITION, out index))
                        {
                            throw new NotImplementedException(""); // TODO: fix
                        }
                        target.Position = index;
                    }

                    {
                        int index;
                        if (t.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.NORMAL, out index))
                        {
                            target.Normal = index;
                        }
                    }

                    {
                        int index;
                        if (t.TryGetValue(Types.Mesh.PrimitiveType.AttributeName.TANGENT, out index))
                        {
                            target.Tangent = index;
                        }
                    }

                    targets.Add(target);
                }

                res.Targets = targets;
            }

            return res;
        }

        void ValidateSubPrimitives(Primitive b, Primitive p, int i)
        {
            if (!EqualityComparer<int?>.Default.Equals(b.Position, p.Position))
            {
                throw new Exception($"Position is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.Normal, p.Normal))
            {
                throw new Exception($"Normal is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.Tangent, p.Tangent))
            {
                throw new Exception($"Tangent is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.TexCoord0, p.TexCoord0))
            {
                throw new Exception($"TexCoord0 is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.TexCoord1, p.TexCoord1))
            {
                throw new Exception($"TexCoord1 is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.Color, p.Color))
            {
                throw new Exception($"Color is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.Weight, p.Weight))
            {
                throw new Exception($"Weight is not same index at: {i}");
            }
            if (!EqualityComparer<int?>.Default.Equals(b.Joint, p.Joint))
            {
                throw new Exception($"Joint is not same index at: {i}");
            }
        }

        async Task<PrimitiveResource> ImportPrimitive(Types.Mesh gltfMesh, Primitive prim, bool isPrimary, CancellationToken ct)
        {
            var res = new PrimitiveResource();

            if (prim.Indices != null)
            {
                res.Indices = ImportIndices(prim.Indices.Value);
            }

            if (prim.Material != null)
            {
                var materialRes = await Context.Importers.Materials.Import(prim.Material.Value, ct);
                await Context.TimeSlicer.Slice(ct);

                res.Material = materialRes.Value;
            }

            if (isPrimary && prim.Position != null)
            {
                res.Vertices = ImportPositions(prim.Position.Value);
            }

            if (isPrimary && prim.Normal != null)
            {
                res.Normals = ImportNormals(prim.Normal.Value);
            }

            if (isPrimary && prim.Tangent != null)
            {
                res.Tangents = ImportTangents(prim.Tangent.Value);
            }

            if (isPrimary && prim.TexCoord0 != null)
            {
                res.UV = ImportUV(prim.TexCoord0.Value);
            }

            if (isPrimary && prim.TexCoord1 != null)
            {
                res.UV2 = ImportUV(prim.TexCoord1.Value);
            }

            if (isPrimary && prim.Color != null)
            {
                res.Colors = ImportColors(prim.Color.Value);
            }

            if (isPrimary && (prim.Joint != null && prim.Weight != null))
            {
                var joints = ImportJoints(prim.Joint.Value);
                var weights = ImportWeights(prim.Weight.Value);
                if (joints.Length != weights.Length)
                {
                    throw new NotImplementedException(); // TODO
                }

                res.BoneWeights = joints.Zip(weights, (j, w) =>
                {
                    var bw = new BoneWeight();
                    bw.boneIndex0 = j.x;
                    bw.boneIndex1 = j.y;
                    bw.boneIndex2 = j.z;
                    bw.boneIndex3 = j.w;
                    bw.weight0 = w.x;
                    bw.weight1 = w.y;
                    bw.weight2 = w.z;
                    bw.weight3 = w.w;
                    return bw;
                }).ToArray();
            }

            if (prim.Targets != null)
            {
                // https://www.khronos.org/registry/glTF/specs/2.0/glTF-2.0.html#morph-targets
                gltfMesh.TryGetExtra<string[]>("targetNames", Context.Container.JsonSchemas, out var targetNames);

                var blendSpapes = new List<BlendShapeResource>();
                var i = 0;
                foreach (var t in prim.Targets)
                {
                    var deltaVertices = ImportPositions(t.Position);

                    var deltaNormals = default(Vector3[]);
                    if (t.Normal != null)
                    {
                        deltaNormals = ImportNormals(t.Normal.Value);
                    }

                    var deltaTangents = default(Vector3[]);
                    if (t.Tangent != null)
                    {
                        // TODO: read Tangents as Vector3[] (NOT Vector4[])
                        //mesh.tangents = ImportTangents(t.Tangent.Value);
                    }

                    var name = (targetNames != null && i < targetNames.Length)
                        ? targetNames[i]
                        : string.Format("BlendShape.{0}", i);
                    blendSpapes.Add(new BlendShapeResource
                    {
                        Name = name,
                        Vertices = deltaVertices,
                        Normals = deltaNormals,
                        Tangents = deltaTangents,
                    });

                    ++i;
                }

                res.BlendShapes = blendSpapes.ToArray();
            }

            return res;
        }

        int[] ImportIndices(int index)
        {
            // SCALAR | !FLOAT
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Scalar)
            {
                if (acc.ComponentType != Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return Context.CoordUtils.FlipIndices(buf.GetPrimitivesAsCasted<int>().ToArray()).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        // https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#meshes

        Vector3[] ImportPositions(int index)
        {
            // VEC3 | FLOAT
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec3)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector3>(PrimitiveImporter.AsVector3).GetEnumerable().Select(Context.CoordUtils.ConvertSpace).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Vector3[] ImportNormals(int index)
        {
            // VEC3 | FLOAT
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec3)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector3>(PrimitiveImporter.AsVector3).GetEnumerable().Select(Context.CoordUtils.ConvertSpace).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Vector4[] ImportTangents(int index)
        {
            // VEC4 | FLOAT
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec4)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector4>(PrimitiveImporter.AsVector4).GetEnumerable().Select(Context.CoordUtils.ConvertSpace).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Vector2[] ImportUV(int index)
        {
            // VEC2 | FLOAT
            //      | UNSIGNED_BYTE  (normalized)
            //      | UNSIGNED_SHORT (normalized)
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec2)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector2>(PrimitiveImporter.AsVector2).GetEnumerable().Select(Context.CoordUtils.ConvertUV).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Color[] ImportColors(int index)
        {
            // VEC3 | FLOAT
            // VEC4 | UNSIGNED_BYTE  (normalized)
            //      | UNSIGNED_SHORT (normalized)
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec4)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector4>(PrimitiveImporter.AsVector4).GetEnumerable().Select(ValueConv.ColorFromSRGB).ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Vec4<int>[] ImportJoints(int index)
        {
            // VEC4 | UNSIGNED_BYTE
            //      | UNSIGNED_SHORT
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec4)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                {
                    return buf.GetEntity<ushort, Vec4<int>>(xs => new Vec4<int>(xs[0], xs[1], xs[2], xs[3]))
                        .GetEnumerable()
                        .ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }

        Vector4[] ImportWeights(int index)
        {
            // VEC4 | FLOAT
            //      | UNSIGNED_BYTE  (normalized)
            //      | UNSIGNED_SHORT (normalized)
            var buf = Context.GltfResources.GetOrLoadTypedBufferByAccessorIndex(index);
            var acc = buf.Accessor;
            if (acc.Type == Types.Accessor.TypeEnum.Vec4)
            {
                if (acc.ComponentType == Types.Accessor.ComponentTypeEnum.FLOAT)
                {
                    return buf.GetEntity<float, Vector4>(PrimitiveImporter.AsVector4).GetEnumerable().ToArray();
                }
            }

            throw new NotImplementedException(); // TODO
        }
    }
}
