#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridPathfinding))]
public class GridPathfindingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 인스펙터 표시
        DrawDefaultInspector();

        GridPathfinding pf = (GridPathfinding)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("에디터 전용 기능", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("그리드 스캔", GUILayout.Height(30)))
            {
                //if (Application.isPlaying)
                //{
                //    Debug.LogWarning("에디터 모드에서만 실행 가능합니다!");
                //}
                //else
                //{
                //    pf.ScanGrid();
                //    SceneView.RepaintAll();
                //}
                pf.ScanGrid();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button(" 경로  찾기 ", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning("에디터 모드에서만 실행 가능합니다!");
                }
                else if (pf.agentObject == null || pf.target == null)
                {
                    Debug.LogWarning("에이전트와 타겟을 먼저 할당하세요!");
                }
                else
                {
                    pf.CalculateAgentBounds();
                    pf.FindPath(pf.agentObject.transform.position, pf.target.position);
                    SceneView.RepaintAll();
                }
            }
        }

        EditorGUILayout.Space(5);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("  에셋에 저장  ", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    Debug.LogWarning("에디터 모드에서만 저장 가능합니다!");
                }
                else
                {
                    pf.SaveGridToAsset();
                }
            }

            if (GUILayout.Button("에셋에서 로드", GUILayout.Height(30)))
            {
                pf.LoadGridFromAsset();
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("새 GridData 에셋 생성", GUILayout.Height(30)))
        {
            CreateNewGridDataAsset(pf);
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드: 시각화만 표시됩니다", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void CreateNewGridDataAsset(GridPathfinding pf)
    {
        GridData newAsset = ScriptableObject.CreateInstance<GridData>();

        string path = EditorUtility.SaveFilePanelInProject(
            "새 GridData 에셋 생성",
            "NewGridData.asset",
            "asset",
            "GridData 에셋을 저장할 위치를 선택하세요");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            pf.gridDataAsset = newAsset;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newAsset;
            Debug.Log($"새 GridData 에셋 생성: {path}");
        }
    }
}
#endif