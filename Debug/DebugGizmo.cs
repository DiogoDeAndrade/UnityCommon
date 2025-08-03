using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace UC
{

    public class DebugGizmo : MonoBehaviour
    {
        public const uint MeshDrawWire = 1;
        public const uint MeshDrawSolid = 2;

        public enum InteractionMode { None, Triangle, Sphere };

        private static DebugGizmo _instance;
        private static DebugGizmo instance
        {
            get
            {
                if (_instance) return _instance;

                _instance = FindAnyObjectByType<DebugGizmo>();

                return _instance;

            }
        }

        public bool filter = true;
        [ShowIf("filter"), TextArea]
        public string filterString = "";
        [Range(0.1f, 4.0f)]
        public float gizmoScale = 1.0f;

        public InteractionMode meshDebugMode;

        abstract class DebugObject
        {
            string _id;
            List<string> identifiers;

            public string identifier
            {
                set
                {
                    _id = value;
                    identifiers = null;
                }
                get
                {
                    return _id;
                }
            }

            public abstract void Draw(float s);

            public bool PassFilter(List<string> filters)
            {
                if (_id == "") return true;
                if (filters == null) return true;

                if ((identifiers == null) || (identifiers.Count == 0))
                {
                    string str = _id.Trim();
                    str = str.ToLower();
                    str = str.Replace(" ", "");
                    str = str.Replace("\t", "");
                    identifiers = new List<string>(str.Split(';'));
                }

                bool pass = true;
                foreach (var f in filters)
                {
                    if (f == "") continue;
                    if (!identifiers.Contains(f))
                    {
                        pass = false;
                        break;
                    }
                }

                return pass;
            }
        }

        class DebugSphere : DebugObject
        {
            public Vector3 position;
            public float radius;
            public Color color;

            public override void Draw(float s)
            {
                Gizmos.color = color;
                Gizmos.DrawSphere(position, radius * s);
            }
        }

        class DebugLine : DebugObject
        {
            public Vector3 p1, p2;
            public Color color;

            public override void Draw(float s)
            {
                Gizmos.color = color;
                Gizmos.DrawLine(p1, p2);
            }
        }

        class DebugMesh : DebugObject
        {
            public Mesh mesh;
            public Color color;
            public Matrix4x4 matrix;
            public uint flags;

            public override void Draw(float s)
            {
                var prevMatrix = Gizmos.matrix;

                Gizmos.matrix = matrix;
                if ((flags & MeshDrawSolid) != 0)
                {
                    Gizmos.color = color;
                    Gizmos.DrawMesh(mesh);
                }
                if ((flags & MeshDrawWire) != 0)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireMesh(mesh);
                }
                Gizmos.matrix = prevMatrix;
            }
        }

        class DebugWireTriangle : DebugObject
        {
            public Triangle triangle;
            public Color color;

            public override void Draw(float s)
            {
                Gizmos.color = color;
                Gizmos.DrawLine(triangle.Gefloat(0), triangle.Gefloat(1));
                Gizmos.DrawLine(triangle.Gefloat(1), triangle.Gefloat(2));
                Gizmos.DrawLine(triangle.Gefloat(2), triangle.Gefloat(0));
            }
        }

        class DebugWireOBB : DebugObject
        {
            public OBB obb;
            public Color color;
            public Matrix4x4 matrix;

            public override void Draw(float s)
            {
                var prevMatrix = Gizmos.matrix;
                Gizmos.color = color;
                Gizmos.matrix = matrix;
                obb.DrawGizmo();
                Gizmos.matrix = prevMatrix;
            }
        }

        List<DebugObject> debugObjects;
        List<DebugObject> highlightObjects;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        [Button("Clear")]
        void _Clear()
        {
            debugObjects = new List<DebugObject>();
        }
        void _Clear(string identifier)
        {
            if (debugObjects == null) return;

            debugObjects.RemoveAll((x) => x == null);
            debugObjects.RemoveAll((x) => x.identifier == identifier);
        }

        void AddObject(DebugObject d)
        {
            if (debugObjects == null) debugObjects = new List<DebugObject>();
            debugObjects.Add(d);
        }

        public static void AddSphere(string identifier, Vector3 pos, float radius, Color color)
        {
            if (instance == null) return;
            instance.AddObject(new DebugSphere()
            {
                identifier = identifier,
                position = pos,
                radius = radius,
                color = color
            });
        }

        public static void AddSphere(string identifier, Vector3 pos, float radius, Color color, Matrix4x4 matrix)
        {
            if (instance == null) return;
            instance.AddObject(new DebugSphere()
            {
                identifier = identifier,
                position = matrix * new Vector4(pos.x, pos.y, pos.z, 1),
                radius = radius,
                color = color
            });
        }

        public static void AddLine(string identifier, Vector3 p1, Vector3 p2, Color color)
        {
            if (instance == null) return;
            instance.AddObject(new DebugLine()
            {
                identifier = identifier,
                p1 = p1,
                p2 = p2,
                color = color
            });
        }

        public static void AddTriangle(string identifier, Triangle triangle, Color color)
        {
            if (instance == null) return;
            instance.AddObject(new DebugWireTriangle()
            {
                identifier = identifier,
                triangle = triangle,
                color = color
            });
        }

        public static void AddTriangle(string identifier, Triangle triangle, Color color, Matrix4x4 matrix)
        {
            if (instance == null) return;
            instance.AddObject(new DebugWireTriangle()
            {
                identifier = identifier,
                triangle = triangle * matrix,
                color = color
            });
        }

        public static void AddWireOBB(string identifier, OBB obb, Color color)
        {
            AddWireOBB(identifier, obb, color, Matrix4x4.identity);
        }

        public static void AddWireOBB(string identifier, OBB obb, Color color, Matrix4x4 matrix)
        {
            if (instance == null) return;

            instance.AddObject(new DebugWireOBB()
            {
                identifier = identifier,
                obb = obb,
                matrix = matrix,
                color = color
            });
        }

        public static void AddLine(string identifier, Vector3 p1, Vector3 p2, Color color, Matrix4x4 matrix)
        {
            if (instance == null) return;
            instance.AddObject(new DebugLine()
            {
                identifier = identifier,
                p1 = matrix * new Vector4(p1.x, p1.y, p1.z, 1),
                p2 = matrix * new Vector4(p2.x, p2.y, p2.z, 1),
                color = color
            });
        }

        public static void AddMesh(string identifier, Mesh mesh, Color color, uint flags)
        {
            AddMesh(identifier, mesh, color, Matrix4x4.identity, flags);
        }

        public static void AddMesh(string identifier, Mesh mesh, Color color, Matrix4x4 matrix, uint flags = MeshDrawSolid)
        {
            if (instance == null) return;
            instance.AddObject(new DebugMesh()
            {
                identifier = identifier,
                color = color,
                mesh = mesh,
                flags = flags,
                matrix = matrix
            });
        }

        public static void Clear()
        {
            if (instance == null) return;

            instance._Clear();
        }

        public static void Clear(string identifier)
        {
            if (instance == null) return;

            instance._Clear(identifier);
        }

        public static bool isAvailable => (instance != null);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (debugObjects != null)
            {
                Event e = Event.current;
                if ((meshDebugMode != InteractionMode.None) && (!Application.isPlaying) && (e.button == 0) && (e.isMouse))
                {
                    if ((meshDebugMode == InteractionMode.Triangle) || (meshDebugMode == InteractionMode.Sphere))
                    {
                        // Clear highlighted triangles
                        highlightObjects = new List<DebugObject>();
                    }
                }

                var fs = filterString;
                fs = fs.Replace(" ", "");
                fs = fs.Replace("\t", "");
                fs = fs.Replace(";", "\n");
                fs = fs.ToLower();
                var filters = new List<string>(fs.Split('\n'));
                filters.ForEach((s) => s.Trim());

                foreach (var obj in debugObjects)
                {
                    if ((!filter) || (obj.PassFilter(filters)))
                    {
                        obj.Draw(gizmoScale);

                        if ((meshDebugMode != InteractionMode.None) && (!Application.isPlaying) && (e.button == 0) && (e.isMouse))
                        {
                            var mp = Event.current.mousePosition;
                            mp.y = UnityEditor.SceneView.lastActiveSceneView.camera.pixelHeight - mp.y;
                            Ray ray = UnityEditor.SceneView.lastActiveSceneView.camera.ScreenPointToRay(mp);
                            Ray rayWorldSpace = ray;

                            DebugMesh dm = obj as DebugMesh;
                            if ((meshDebugMode == InteractionMode.Triangle) && (dm != null))
                            {
                                // Raycast this mesh
                                var invMatrix = dm.matrix.inverse;

                                ray.origin = invMatrix * new Vector4(ray.origin.x, ray.origin.y, ray.origin.z, 1);
                                ray.direction = invMatrix * new Vector4(ray.direction.x, ray.direction.y, ray.direction.z, 0);

                                var hits = dm.mesh.RaycastAll(ray.origin, ray.direction, float.MaxValue);
                                if (hits != null)
                                {
                                    foreach (var hit in hits)
                                    {
                                        Debug.Log($"Mesh={obj.identifier} / Submesh={hit.submeshIndex} / Triangle={hit.triIndex} / Distance={hit.t}");
                                        var triangle = dm.mesh.GetTriangle(hit.submeshIndex, hit.triIndex);
                                        triangle = triangle * dm.matrix;
                                        Debug.Log($":: Area={triangle.area}");

                                        highlightObjects.Add(new DebugWireTriangle
                                        {
                                            identifier = "",
                                            triangle = triangle,
                                            color = Color.yellow
                                        });
                                        /*highlightObjects.Add(new DebugSphere
                                        {
                                            identifier = "",
                                            position = rayWorldSpace.origin + rayWorldSpace.direction * hit.t,
                                            radius = 0.025f,
                                            color = Color.magenta
                                        });*/
                                    }
                                }
                            }
                            DebugSphere ds = obj as DebugSphere;
                            if ((meshDebugMode == InteractionMode.Sphere) && (ds != null))
                            {
                                float t;
                                if (Sphere.Raycast(ray, ds.position, ds.radius, float.MaxValue, out t))
                                {
                                    Debug.Log($"Sphere={obj.identifier} / Distance={t}");
                                    highlightObjects.Add(new DebugSphere
                                    {
                                        identifier = "",
                                        position = ds.position,
                                        radius = ds.radius + 0.01f,
                                        color = Color.yellow
                                    });
                                }
                            }
                        }
                    }
                }

                if (highlightObjects != null)
                {
                    foreach (var obj in highlightObjects)
                    {
                        obj.Draw(gizmoScale);
                    }
                }
            }
        }
#endif

    }
}