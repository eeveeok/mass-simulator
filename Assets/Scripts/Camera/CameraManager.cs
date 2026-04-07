using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public List<Camera> cameras;
    public Camera sensorCamera;
    public RenderTexture subCameraTexture;
    private int cameraNum;

    public Vector2 subCameraPosition;
    public Vector2 subCameraSize;

    void Awake()
    {
        cameraNum = 0;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            ChangeCameraView();
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            ChangeSensorCameraView();
        }
    }

    void ChangeCameraView()
    {
        if (cameraNum >= cameras.Count) cameraNum = 0;

        Camera mainCamera = cameras[cameraNum++];

        sensorCamera.enabled = false;
        cameras.ForEach(c => {
            c.enabled = true;
            c.targetTexture = null;
        });

        // 현재 카메라를 서브로
        mainCamera.targetTexture = subCameraTexture;
    }

    void ChangeSensorCameraView()
    {
        if (cameraNum >= cameras.Count) cameraNum = 0;

        Camera curCamera = cameras[cameraNum];

        curCamera.enabled = !curCamera.enabled;
        sensorCamera.enabled = !sensorCamera.enabled;
    }
}