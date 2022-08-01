using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class DebugGizmo : MonoBehaviour
{
    private static DebugGizmo _instance;
    private static DebugGizmo instance
    {
        get
        {
            if (_instance) return _instance;

            _instance = FindObjectOfType<DebugGizmo>();
            
            return _instance;

        }
    }

    public bool     filter = true;
    [ShowIf("filter"), TextArea]
    public string   filterString = "";
    [Range(0.1f, 4.0f)]
    public float    gizmoScale = 1.0f;

    abstract class DebugObject
    {
        string          _id;
        List<string>    identifiers;

        public string identifier
        {
            set
            {
                _id = value;
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
        public Vector3  p1, p2;
        public Color    color;

        public override void Draw(float s)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(p1, p2);
        }
    }

    List<DebugObject>       debugObjects;

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
    void Clear()
    {
        debugObjects = new List<DebugObject>();
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
                               p1 = p1, p2 = p2, 
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

    private void OnDrawGizmos()
    {
        if (debugObjects == null) return;

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
            }
        }
    }
}
