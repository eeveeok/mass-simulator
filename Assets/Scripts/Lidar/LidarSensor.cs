using UnityEngine;
using System.Collections.Generic;

public class LidarSensor : MonoBehaviour
{
    public int raysPerScan = 360;
    public float maxDistance = 100f;
    public float rotationSpeed = 30f;
    public LayerMask obstacleLayers;
    public float scanAngle = 180f;    // 스캔 각도 (180도 = 전방 집중)
    public bool showRay = false;

    private List<Vector3> hitPoints = new List<Vector3>();
    private RaycastHit[] raycastHits; // 캐싱을 위한 배열

    void Start()
    {
        raycastHits = new RaycastHit[raysPerScan];
        hitPoints = new List<Vector3>(raysPerScan);
    }

    void Update()
    {
        Scan();
    }

    void Scan()
    {
        hitPoints.Clear();
        float angleIncrement = scanAngle / (raysPerScan - 1);
        float startAngle = -scanAngle / 2;

        for (int i = 0; i < raysPerScan; i++)
        {
            float angle = startAngle + i * angleIncrement;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * -transform.right;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, dir, out hit, maxDistance, obstacleLayers))
            {
                hitPoints.Add(hit.point);

                if(showRay)
                    Debug.DrawLine(transform.position, hit.point, Color.red);
            }
            else
            {
                Debug.DrawRay(transform.position, dir * maxDistance, Color.green);
            }
        }
    }

    public List<Vector3> GetHitPoints() => hitPoints;
}