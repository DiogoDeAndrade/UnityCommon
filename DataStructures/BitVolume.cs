using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UC
{

    public sealed class BitVolume
    {
        public readonly int width;
        public readonly int height;
        public readonly int depth;

        // 1 bit per voxel
        private readonly ulong[] data;

        public BitVolume(int width, int height, int depth)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;

            long bitCount = (long)width * height * depth;
            long wordCount = (bitCount + 63) >> 6;
            data = new ulong[wordCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(int x, int y, int z) => (z * height + y) * width + x;
        private int Index(Vector3Int p) => (p.z * height + p.y) * width + p.x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int x, int y, int z, bool value)
        {
            int idx = Index(x, y, z);
            int word = idx >> 6;
            int bit = idx & 63;
            ulong mask = 1UL << bit;

            if (value) data[word] |= mask;
            else data[word] &= ~mask;
        }

        public void Set(Vector3Int p, bool value)
        {
            int idx = Index(p);
            int word = idx >> 6;
            int bit = idx & 63;
            ulong mask = 1UL << bit;

            if (value) data[word] |= mask;
            else data[word] &= ~mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int x, int y, int z)
        {
            int idx = Index(x, y, z);
            int word = idx >> 6;
            int bit = idx & 63;
            return (data[word] & (1UL << bit)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAll()
        {
            Array.Clear(data, 0, data.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RequireSameDims(BitVolume a, BitVolume b)
        {
            if (a.width != b.width || a.height != b.height || a.depth != b.depth)
                throw new ArgumentException("BitVolume dimension mismatch.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RequireSameDims(BitVolume a, BitVolume b, BitVolume c)
        {
            RequireSameDims(a, b);
            RequireSameDims(a, c);
        }

        public static BitVolume And(BitVolume a, BitVolume b)
        {
            RequireSameDims(a, b);

            var dst = new BitVolume(a.width, a.height, a.depth);
            var da = a.data;
            var db = b.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
                dd[i] = da[i] & db[i];

            return dst;
        }

        public static BitVolume Or(BitVolume a, BitVolume b)
        {
            RequireSameDims(a, b);

            var dst = new BitVolume(a.width, a.height, a.depth);
            var da = a.data;
            var db = b.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
                dd[i] = da[i] | db[i];

            return dst;
        }

        public static BitVolume Not(BitVolume a)
        {
            var dst = new BitVolume(a.width, a.height, a.depth);
            var da = a.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
                dd[i] = ~da[i];

            // Important: mask out unused tail bits so they don't become "true"
            dst.MaskUnusedTailBits();
            return dst;
        }

        public static BitVolume Majority3(BitVolume a, BitVolume b, BitVolume c)
        {
            RequireSameDims(a, b, c);

            var dst = new BitVolume(a.width, a.height, a.depth);
            var da = a.data;
            var db = b.data;
            var dc = c.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
            {
                ulong A = da[i];
                ulong B = db[i];
                ulong C = dc[i];
                dd[i] = (A & B) | (A & C) | (B & C);
            }

            // Tail bits already come from AND/OR combos, but still safe to mask.
            dst.MaskUnusedTailBits();
            return dst;
        }

        public static void AndInto(BitVolume dst, BitVolume a, BitVolume b)
        {
            RequireSameDims(dst, a);
            RequireSameDims(dst, b);

            var da = a.data;
            var db = b.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
                dd[i] = da[i] & db[i];
        }

        public static void Majority3Into(BitVolume dst, BitVolume a, BitVolume b, BitVolume c)
        {
            RequireSameDims(dst, a);
            RequireSameDims(dst, b);
            RequireSameDims(dst, c);

            var da = a.data;
            var db = b.data;
            var dc = c.data;
            var dd = dst.data;

            for (int i = 0; i < dd.Length; i++)
            {
                ulong A = da[i];
                ulong B = db[i];
                ulong C = dc[i];
                dd[i] = (A & B) | (A & C) | (B & C);
            }

            dst.MaskUnusedTailBits();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MaskUnusedTailBits()
        {
            // Number of valid bits
            long bitCount = (long)width * height * depth;
            int usedBitsInLastWord = (int)(bitCount & 63);
            if (usedBitsInLastWord == 0) return; // perfectly aligned

            ulong mask = (1UL << usedBitsInLastWord) - 1UL;
            data[data.Length - 1] &= mask;
        }

        public ulong[] RawWords => data;
    }
}
