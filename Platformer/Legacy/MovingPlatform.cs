using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC.Legacy
{

    public class MovingPlatform : ActivatedComponent
    {
        public float speed;
        public Transform[] waypoints;
        public int startWaypoint = -1;
        public bool loop;
        public Collider2D influenceArea;
        public LayerMask carryLayers;

        Rigidbody2D rb;
        TimeScaler2d timeScaler;
        Vector3 currentTarget;
        Vector3[] positions;
        int index;
        int inc;
        Vector3 prevPos;
        ContactFilter2D contactFilter;

        void Start()
        {
            timeScaler = GetComponent<TimeScaler2d>();

            // Store positions in a variable, so that the waypoints can be relative to the platform (children objects)
            positions = new Vector3[waypoints.Length];
            index = 0;
            foreach (var p in waypoints)
            {
                if (p)
                {
                    positions[index] = p.position;
                    index++;
                }
            }

            index = 0;
            inc = 1;
            currentTarget = positions[index];

            if (startWaypoint != -1)
            {
                transform.position = positions[startWaypoint];
                index = (startWaypoint + 1) % waypoints.Length;
                currentTarget = positions[index];
            }

            rb = GetComponent<Rigidbody2D>();

            contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(carryLayers);
        }

        void FixedUpdate()
        {
            if (Vector3.Distance(transform.position, currentTarget) < 0.5f)
            {
                if (active)
                {
                    index += inc;

                    if (index < 0)
                    {
                        index = 0;
                        inc = 1;
                        if (!loop) active = false;
                    }
                    else if (index >= positions.Length)
                    {
                        index = positions.Length - 1;
                        inc = -1;
                        if (!loop) active = false;
                    }

                    currentTarget = positions[index];
                }
            }
            else
            {
                float maxDistanceDelta = speed * ((timeScaler) ? (timeScaler.fixedDeltaTime) : (Time.fixedDeltaTime));

                if (rb)
                {
                    var toTarget = (currentTarget - transform.position).normalized;

                    rb.linearVelocity = toTarget * speed;
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.position, currentTarget, maxDistanceDelta);
                }
            }

            Vector3 deltaPos = transform.position - prevPos;

            if (influenceArea != null)
            {
                List<Collider2D> results = new List<Collider2D>();
                if (Physics2D.OverlapCollider(influenceArea, contactFilter, results) > 0)
                {
                    // Affected object list
                    List<Rigidbody2D> affectedObjects = new List<Rigidbody2D>();

                    foreach (var collider in results)
                    {
                        var rootRB = collider.GetComponent<Rigidbody2D>();
                        if (rootRB == null) rootRB = collider.GetComponentInParent<Rigidbody2D>();

                        if (affectedObjects.IndexOf(rootRB) != -1) continue;

                        affectedObjects.Add(rootRB);

                        rootRB.transform.position += deltaPos;
                    }
                }
            }

            prevPos = transform.position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool selected = false;

            foreach (var sel in Selection.objects)
            {
                if (sel == gameObject)
                {
                    selected = true;
                    break;
                }

                GameObject go = sel as GameObject;
                if (go)
                {
                    if (go.transform.IsChildOf(transform))
                    {
                        selected = true;
                        break;
                    }
                }
            }

            if (selected)
            {
                Gizmos.color = Color.yellow;

                if ((positions != null) && (positions.Length > 0))
                {
                    for (int i = 0; i < positions.Length - 1; i++)
                    {
                        Gizmos.DrawLine(positions[i], positions[i + 1]);
                    }
                }
                else
                {
                    for (int i = 0; i < waypoints.Length - 1; i++)
                    {
                        if (waypoints[i])
                        {
                            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                        }
                    }
                }
            }
        }
#endif
    }
}