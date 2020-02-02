using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : ActivatedComponent
{
    public  float       speed;
    public  Transform[] waypoints;
    public  bool        loop;
    public  Collider2D  influenceArea;
    public  LayerMask   carryLayers;

    TimeScaler2d    timeScaler;
    Vector3         currentTarget;
    Vector3[]       positions;
    int             index;
    int             inc;
    Vector3         prevPos;
    ContactFilter2D contactFilter;

    void Start()
    {
        timeScaler = GetComponent<TimeScaler2d>();

        // Store positions in a variable, so that the waypoints can be relative to the platform (children objects)
        positions = new Vector3[waypoints.Length];
        index = 0;
        foreach (var p in waypoints)
        {
            positions[index] = p.position;
            index++;
        }

        index = 0;
        inc = 1;

        currentTarget = positions[index];

        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(carryLayers);
    }

    void Update()
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
            float maxDistanceDelta = speed * ((timeScaler) ? (timeScaler.deltaTime) : (Time.deltaTime));

            transform.position = Vector3.MoveTowards(transform.position, currentTarget, maxDistanceDelta);
        }

        Vector3 deltaPos = transform.position - prevPos;

        List<Collider2D> results = new List<Collider2D>();
        if (Physics2D.OverlapCollider(influenceArea, contactFilter, results) > 0)
        {
            // Affected object list
            List<GameObject> affectedObjects = new List<GameObject>();

            foreach (var collider in results)
            {
                var rootObject = collider.gameObject.GetRootObject();

                if (affectedObjects.IndexOf(rootObject) != -1) continue;

                affectedObjects.Add(rootObject);

                rootObject.transform.position += deltaPos;
            }
        }

        prevPos = transform.position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        if (positions != null)
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
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
