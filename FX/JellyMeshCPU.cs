using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    public class JellyMeshCPU : MonoBehaviour
    {
        public enum NoiseType { None, Random, GradientY, VertexColor };
        public enum NormalMode { Source, Fast, Accurate };

        [SerializeField] private NormalMode normalMode = NormalMode.Source;
        [SerializeField, ShowIf(nameof(isAccurateNormals))] private float blendThreshold = 0.1f;
        [SerializeField] private NoiseType noiseType = NoiseType.None;
        [SerializeField, MinMaxSlider(0.0f, 2.0f), ShowIf(nameof(hasNoise))] private Vector2 noiseScale = Vector2.one;
        [SerializeField, ShowIf(nameof(isGradientNoise))] private Vector2 positionLimits = Vector2.zero;
        [SerializeField] private float springStiffness = 200f;   // k
        [SerializeField] private float dampingCoefficient = 10f; // d
        [SerializeField, Layer] private int invisibleLayer;
        [SerializeField] private Material invisibleMaterial;

        private class JellyMeshElem
        {
            public MeshFilter meshFilter;
            public SkinnedMeshRenderer skinnedSource;
            public Mesh bakedMesh;
            public Mesh jellyMesh;
            public Vector3[] srcPositions;           // original local-space mesh
            public Vector3[] originalNormals;        // original local-space normals
            public Vector3[] srcWorldPositions;      // rest pose in world-space (updated each frame)
            public Vector3[] deformedWorldPositions; // deformed jelly shape (world space)
            public Vector3[] deformedLocalNormals;   // final local-space normals
            public Vector3[] prevSrcWorldPositions;  // previous frame's srcWorldPositions
            public Vector3[] velocities;             // world-space velocities
            public Vector3[] localOutput;            // final positions to assign to mesh
            public float[] springPerVertex;

            public void FixedUpdate(NormalMode normalMode, float springStiffness, float dampingCoefficient, float blendThreshold = 0.1f)
            {
                if (skinnedSource)
                {
                    skinnedSource.BakeMesh(bakedMesh);
                    bakedMesh.vertices.CopyTo(srcPositions, 0);
                    bakedMesh.normals.CopyTo(originalNormals, 0);
                }

                meshFilter.transform.TransformPoints(srcPositions, srcWorldPositions);

                float dt = Time.fixedDeltaTime;

                for (int i = 0; i < srcPositions.Length; i++)
                {
                    Vector3 restPos = srcWorldPositions[i];
                    Vector3 currPos = deformedWorldPositions[i];
                    Vector3 velocity = velocities[i];

                    Vector3 displacement = restPos - currPos;
                    Vector3 springForce = displacement * springStiffness;
                    Vector3 dampingForce = -velocity * dampingCoefficient;

                    Vector3 acceleration = (springForce * springPerVertex[i]) + dampingForce;

                    velocity += acceleration * dt;
                    currPos += velocity * dt;

                    // Store back
                    velocities[i] = velocity;
                    deformedWorldPositions[i] = currPos;
                    localOutput[i] = meshFilter.transform.InverseTransformPoint(currPos);
                }

                // Store for next frame
                srcWorldPositions.CopyTo(prevSrcWorldPositions, 0);

                jellyMesh.vertices = localOutput;

                switch (normalMode)
                {
                    case NormalMode.Source:
                        break;
                    case NormalMode.Fast:
                        jellyMesh.RecalculateNormals();
                        jellyMesh.RecalculateTangents();
                        break;
                    case NormalMode.Accurate:
                        {
                            float b2 = 1.0f / (blendThreshold * blendThreshold);
                            jellyMesh.RecalculateNormals();
                            var newNormals = jellyMesh.normals;
                            for (int i = 0; i < newNormals.Length; i++)
                            {
                                float distSq = (deformedWorldPositions[i] - srcWorldPositions[i]).sqrMagnitude;
                                float weight = Mathf.Clamp01(distSq * b2);

                                deformedLocalNormals[i] = Vector3.Slerp(originalNormals[i], newNormals[i], weight);
                            }
                            jellyMesh.normals = deformedLocalNormals;

                            jellyMesh.RecalculateTangents();
                        }
                        break;
                    default:
                        break;
                }
                jellyMesh.RecalculateBounds();
            }
        }

        private List<JellyMeshElem> jellyMeshes;

        bool hasNoise => noiseType != NoiseType.None;
        bool isGradientNoise => noiseType == NoiseType.GradientY;
        bool isAccurateNormals => normalMode == NormalMode.Accurate;

        JellyMeshElem CreateJellyMesh(MeshFilter meshFilter, Mesh mesh, int vertexCount)
        {
            var elem = new JellyMeshElem()
            {
                meshFilter = meshFilter,
                srcPositions = mesh.vertices,
                originalNormals = mesh.normals,
                srcWorldPositions = new Vector3[vertexCount],
                prevSrcWorldPositions = new Vector3[vertexCount],
                deformedWorldPositions = new Vector3[vertexCount],
                deformedLocalNormals = new Vector3[vertexCount],
                velocities = new Vector3[vertexCount],
                localOutput = new Vector3[vertexCount],
            };
            jellyMeshes.Add(elem);

            // Compute initial world-space positions
            transform.TransformPoints(elem.srcPositions, elem.srcWorldPositions);
            elem.srcWorldPositions.CopyTo(elem.deformedWorldPositions, 0);
            elem.srcWorldPositions.CopyTo(elem.prevSrcWorldPositions, 0);

            // Create jelly mesh
            elem.jellyMesh = MeshTools.CopyMesh(mesh);
            elem.jellyMesh.MarkDynamic();
            meshFilter.mesh = elem.jellyMesh;

            elem.springPerVertex = new float[vertexCount];
            switch (noiseType)
            {
                case NoiseType.None:
                    for (int i = 0; i < vertexCount; i++) elem.springPerVertex[i] = 1.0f;
                    break;
                case NoiseType.Random:
                    for (int i = 0; i < vertexCount; i++) elem.springPerVertex[i] = Random.Range(noiseScale.x, noiseScale.y);
                    break;
                case NoiseType.GradientY:
                    for (int i = 0; i < vertexCount; i++)
                    {
                        float yNorm = Mathf.Clamp01((elem.srcPositions[i].y - positionLimits.x) / (positionLimits.y - positionLimits.x));

                        elem.springPerVertex[i] = Mathf.Lerp(noiseScale.x, noiseScale.y, yNorm);
                    }
                    break;
                case NoiseType.VertexColor:
                    var colors = mesh.colors;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        elem.springPerVertex[i] = Mathf.Lerp(noiseScale.x, noiseScale.y, colors[i].r);
                    }
                    break;
                default:
                    break;
            }

            return elem;
        }

        void Start()
        {
            var meshFilters = GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length > 0)
            {
                jellyMeshes = new();
                foreach (var meshFilter in meshFilters)
                {
                    var mesh = meshFilter.sharedMesh;
                    var vertexCount = meshFilter.sharedMesh.vertexCount;

                    CreateJellyMesh(meshFilter, mesh, vertexCount);
                }
            }

            var skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinnedMeshes.Length > 0)
            {
                if (jellyMeshes == null) jellyMeshes = new();
                foreach (var skinnedMesh in skinnedMeshes)
                {
                    // Need to create an object to render the baked skinned mesh
                    GameObject go = new GameObject();
                    go.name = skinnedMesh.name + "_JellyMesh";
                    go.transform.SetParent(skinnedMesh.transform, false);
                    var meshRenderer = go.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterials = skinnedMesh.sharedMaterials;
                    var meshFilter = go.AddComponent<MeshFilter>();

                    Mesh mesh = new Mesh();
                    skinnedMesh.BakeMesh(mesh, false);
                    var elem = CreateJellyMesh(meshFilter, mesh, mesh.vertexCount);
                    elem.skinnedSource = skinnedMesh;
                    elem.bakedMesh = mesh;

                    if (invisibleMaterial)
                    {
                        skinnedMesh.materials = new Material[] { invisibleMaterial };
                    }
                    else
                    {
                        skinnedMesh.gameObject.layer = invisibleLayer;
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (jellyMeshes != null)
            {
                foreach (var jellyMesh in jellyMeshes)
                {
                    jellyMesh.FixedUpdate(normalMode, springStiffness, dampingCoefficient, blendThreshold);
                }
            }
        }
    }
}