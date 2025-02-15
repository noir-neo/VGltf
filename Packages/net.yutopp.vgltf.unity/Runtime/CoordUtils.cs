﻿//
// Copyright (c) 2019- yutopp (yutopp@gmail.com)
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at  https://www.boost.org/LICENSE_1_0.txt)
//

using System;
using System.Collections.Generic;
using UnityEngine;

namespace VGltf.Unity
{
    /// <summary>
    /// Convert coordinates.
    ///   Unity : Left hand  / +Y up / +Z front
    ///   glTF  : Right hand / +Y up / +Z front
    /// Conversion
    ///   Axis       : +X <-> -X
    ///   Indices    : Flip order
    ///   Rotation   : Flip sign
    ///   UV         : U’=U, V’=1-V
    ///   Handedness : Flip sign
    /// </summary>
    public sealed class CoordUtils
    {
        readonly Vector3 CoordinateSpaceAxisFlip = new Vector3(-1, 1, 1); // +X -> -X

        public CoordUtils() { }

        public CoordUtils(Vector3 axis)
        {
            CoordinateSpaceAxisFlip = axis;
        }

        public IEnumerable<int> FlipIndices(int[] xs)
        {
            if (xs.Length % 3 != 0)
            {
                throw new NotImplementedException(); // TODO:
            }

            for (int i = 0; i < xs.Length / 3; ++i)
            {
                // From : (0, 1, 2), (3, 4, 5), ...
                // To   : (2, 1, 0), (5, 4, 3), ...
                yield return xs[i * 3 + 2];
                yield return xs[i * 3 + 1];
                yield return xs[i * 3 + 0];
            }
        }

        public Vector2 ConvertUV(Vector2 v)
        {
            // From : (u, v)
            // To   : (u, 1 - v)
            return new Vector2(v.x, 1 - v.y);
        }

        public Vector3 ConvertSpace(Vector3 v)
        {
            return new Vector3(v.x * CoordinateSpaceAxisFlip.x, v.y * CoordinateSpaceAxisFlip.y, v.z * CoordinateSpaceAxisFlip.z);
        }

        public Vector4 ConvertSpace(Vector4 v)
        {
            return new Vector4(v.x * CoordinateSpaceAxisFlip.x, v.y * CoordinateSpaceAxisFlip.y, v.z * CoordinateSpaceAxisFlip.z, v.w * -1);
        }

        public Quaternion ConvertSpace(Quaternion q)
        {
            // https://stackoverflow.com/questions/41816497/right-hand-camera-to-left-hand-opencv-to-unity
            return new Quaternion(q.x * -CoordinateSpaceAxisFlip.x, q.y * -CoordinateSpaceAxisFlip.y, q.z * -CoordinateSpaceAxisFlip.z, q.w * -(-1));
        }

        public Matrix4x4 ConvertSpace(Matrix4x4 m)
        {
            //
            // NOTE: Calcurated matrix will be BROKEN, when scale or rotation is NOT uniformed.
            // At first, this logic decompose matrices into TRS, and convert coordinates, then compose them again.
            // However, the values of scale or rotation cannot be determined uniquely when decomposing.
            //
            // Hint: Use `VGltf.Unity.Ext.TransformNormalizer` to satisfy the constrants.
            //

            var t = GetTranslate(m);
            var r = GetRotation(m);
            var s = GetScale(m);
            if (s != Vector3.one)
            {
                Debug.LogWarningFormat("Scale or Rotation should be identity: Actual = {0}", s.ToString("G7"));
            }

            return Matrix4x4.TRS(ConvertSpace(t), ConvertSpace(r), s);
        }

        // https://answers.unity.com/questions/402280/how-to-decompose-a-trs-matrix.html

        public static Vector3 GetTranslate(Matrix4x4 m)
        {
            return m.GetColumn(3);
        }

        public static Quaternion GetRotation(Matrix4x4 m)
        {
            var r = Quaternion.LookRotation(
                m.GetColumn(2),
                m.GetColumn(1)
                );

            return r;
        }

        public static Vector3 GetScale(Matrix4x4 m)
        {
            var s = new Vector3(
                m.GetColumn(0).magnitude,
                m.GetColumn(1).magnitude,
                m.GetColumn(2).magnitude
                );

            return s;
        }
    }
}
