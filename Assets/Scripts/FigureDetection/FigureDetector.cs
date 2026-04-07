using OpenCvSharp;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class FigureDetector : MonoBehaviour
{
    public GameObject[] quadObjects;
    public string[] imageNames = { "Square", "Circle", "Triangle" };
    public LayerMask quadLayerMask; // Quad 오브젝트만 인식하도록 레이어 마스크

    private Mat template;
    private Mat lastScreenMat;
    private Texture2D selectedTexture;
    private RenderTexture renderTexture;
    private int selectedIndex;
    private Camera targetCamera;

    void Start()
    {
        targetCamera = GetComponent<Camera>();

        // Resources 폴더 존재 확인
        if (!System.IO.Directory.Exists(Application.dataPath + "/Resources"))
        {
            Debug.LogError("Resources 폴더가 존재하지 않습니다!");
            return;
        }

        selectedIndex = UnityEngine.Random.Range(0, imageNames.Length);
        LoadTemplateImage(imageNames[selectedIndex]);
        ApplyToQuad(quadObjects[selectedIndex]);

        renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            FigureDetect();
        }
    }

    void LoadTemplateImage(string imageName)
    {
        // 이미지 로드
        selectedTexture = Resources.Load<Texture2D>("Textures/" + imageName);

        if (selectedTexture == null)
        {
            Debug.LogError("이미지 로드 실패: Textures/" + imageName);
            return;
        }

        Debug.Log($"텍스처 로드 성공: {imageName} ({selectedTexture.width}x{selectedTexture.height})");

        template = TextureToMat(selectedTexture);
        if (template == null || template.Empty())
        {
            Debug.LogError("템플릿 변환 실패!");
            return;
        }
    }

    // 안전한 텍스처 -> Mat 변환
    Mat TextureToMat(Texture2D texture)
    {
        try
        {
            // 임시 렌더 텍스처 생성
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;

            // 임시 텍스처 생성 (명시적 RGB24 포맷)
            Texture2D tempTex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tempTex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0);
            tempTex.Apply();

            // 픽셀 데이터 추출
            Color32[] pixels = tempTex.GetPixels32();
            byte[] data = new byte[pixels.Length * 3]; // 3채널(BGR)

            // Color32[] -> byte[] 변환 (RGB -> BGR)
            for (int i = 0; i < pixels.Length; i++)
            {
                data[i * 3 + 0] = pixels[i].b; // Blue
                data[i * 3 + 1] = pixels[i].g; // Green
                data[i * 3 + 2] = pixels[i].r; // Red
            }

            // Mat 생성 (명시적 타입 지정)
            Mat mat = new Mat(texture.height, texture.width, MatType.CV_8UC3, data);

            // 리소스 정리
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(tempTex);

            return mat;
        }
        catch (Exception e)
        {
            Debug.LogError($"텍스처 변환 실패: {e.Message}");
            return null;
        }
    }

    void ApplyToQuad(GameObject quad)
    {
        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null && selectedTexture != null)
        {
            renderer.material.mainTexture = selectedTexture;
        }
    }

    public GameObject FigureDetect()
    {
        if (template == null || template.Empty())
        {
            Debug.LogWarning("템플릿 이미지가 없습니다!");
            return null;
        }

        // 매칭 시 반환될 도형
        GameObject targetFigure = null;

        // 현재 카메라 설정 백업
        RenderTexture originalTarget = targetCamera.targetTexture;
        CameraClearFlags originalClearFlags = targetCamera.clearFlags;
        Color originalBackground = targetCamera.backgroundColor;

        try
        {
            // 1. 화면 캡처
            RenderTexture previousRT = RenderTexture.active;
            RenderTexture tempRT = new RenderTexture(Screen.width, Screen.height, 24);

            Texture2D screenShot = new Texture2D(tempRT.width, tempRT.height, TextureFormat.RGB24, false);
            screenShot.ReadPixels(new UnityEngine.Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            screenShot.Apply();

            RenderTexture.active = previousRT;

            // 2. Mat 변환
            Mat screenMat = TextureToMat(screenShot);
            lastScreenMat = screenMat.Clone();

            if (screenMat == null || screenMat.Empty())
            {
                Debug.LogWarning("화면 캡처 실패!");
                Cleanup(tempRT, screenShot);
                return null;
            }

            // 3. 템플릿 리사이징 (Quad의 화면 상 크기 계산)
            GameObject quad = quadObjects[selectedIndex];
            Renderer quadRenderer = quad.GetComponent<Renderer>();

            if (quadRenderer == null)
            {
                Debug.LogError("Quad에 Renderer 컴포넌트가 없습니다!");
                return null;
            }

            // Quad의 월드 공간 바운딩 박스 계산
            Bounds bounds = quadRenderer.bounds;

            // Quad의 모서리 좌표를 화면 좌표로 변환
            Vector3[] screenCorners = new Vector3[8];
            screenCorners[0] = targetCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.min.z));
            screenCorners[1] = targetCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
            screenCorners[2] = targetCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
            screenCorners[3] = targetCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
            screenCorners[4] = targetCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
            screenCorners[5] = targetCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            screenCorners[6] = targetCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
            screenCorners[7] = targetCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.max.z));

            // 화면상 Quad 크기 계산
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (Vector3 corner in screenCorners)
            {
                if (corner.z > 0) // 카메라 앞에 있는 경우만 처리
                {
                    minX = Mathf.Min(minX, corner.x);
                    maxX = Mathf.Max(maxX, corner.x);
                    minY = Mathf.Min(minY, corner.y);
                    maxY = Mathf.Max(maxY, corner.y);
                }
            }

            int quadWidth = Mathf.RoundToInt(maxX - minX);
            int quadHeight = Mathf.RoundToInt(maxY - minY);

            // 크기 유효성 검사
            if (quadWidth <= 0 || quadHeight <= 0)
            {
                Debug.LogError("유효하지 않은 Quad 크기! 카메라 화면 안에 있는지 확인하세요.");
                return null;
            }

            Mat resizedTemplate = new Mat();
            Cv2.Resize(template, resizedTemplate, new Size(quadWidth, quadHeight));

            // 4. 그레이스케일 변환 + 평활화
            Mat grayTemplate = new Mat();
            Mat grayScreen = new Mat();

            Cv2.CvtColor(resizedTemplate, grayTemplate, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(screenMat, grayScreen, ColorConversionCodes.BGR2GRAY);

            // 대비 개선
            Cv2.EqualizeHist(grayTemplate, grayTemplate);
            Cv2.EqualizeHist(grayScreen, grayScreen);

            // 5. 템플릿 매칭
            using (Mat result = new Mat())
            {
                try
                {
                    Cv2.MatchTemplate(grayScreen, grayTemplate, result, TemplateMatchModes.CCoeffNormed);
                }
                catch (Exception e)
                {
                    Debug.LogError($"매칭 오류: {e.Message}");
                    return null;
                }

                double minVal, maxVal;
                Point minLoc, maxLoc;
                Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);

                Debug.Log($"매칭 결과: 최대값={maxVal:F4} | 위치=({maxLoc.X},{maxLoc.Y})");

                // 개선된 매칭 검증: 주변 3x3 픽셀 평균값 사용
                double avgVal = 0;
                int count = 0;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (maxLoc.X + x >= 0 && maxLoc.Y + y >= 0 &&
                            maxLoc.X + x < result.Width && maxLoc.Y + y < result.Height)
                        {
                            avgVal += result.At<float>(maxLoc.Y + y, maxLoc.X + x);
                            count++;
                        }
                    }
                }
                avgVal /= count;

                // 임계값 조정 (0.5 -> 0.6)
                if (avgVal > 0.5)
                {
                    // 좌표 변환 보정
                    Vector3 screenPos = new Vector3(
                        maxLoc.X + resizedTemplate.Width / 2,
                        Screen.height - (maxLoc.Y + resizedTemplate.Height / 2),
                        targetCamera.nearClipPlane + 1
                    );

                    targetFigure = HighlightQuad(screenPos);
                }
                else
                {
                    Debug.Log($"매칭 점수 부족: {avgVal:F4}");
                }
            }

            // 리소스 정리
            Cleanup(tempRT, screenShot);
            grayScreen.Dispose();
            grayTemplate.Dispose();
            resizedTemplate.Dispose();
        }
        finally
        {
            // 8. 원래 카메라 설정 복원 (finally 블록으로 보장)
            targetCamera.targetTexture = originalTarget;
            targetCamera.clearFlags = originalClearFlags;
            targetCamera.backgroundColor = originalBackground;
        }

        return targetFigure;
    }

    void Cleanup(RenderTexture rt, Texture2D tex)
    {
        RenderTexture.active = null;
        targetCamera.targetTexture = null;
        Destroy(tex);
        rt.Release();
    }

    GameObject HighlightQuad(Vector3 screenPosition)
    {
        // 1. 스크린 좌표에서 월드 좌표로 직접 변환
        Vector3 worldPos = targetCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, targetCamera.nearClipPlane)
        );

        // 2. 카메라에서 Quad 방향으로 레이 생성
        Vector3 rayDirection = (quadObjects[selectedIndex].transform.position - targetCamera.transform.position).normalized;
        Ray ray = new Ray(targetCamera.transform.position, rayDirection);

        Debug.DrawRay(ray.origin, ray.direction * 100, Color.red, 2f); // 디버그용 레이 시각화

        // 3. 레이캐스트 설정
        float maxDistance = Vector3.Distance(targetCamera.transform.position, quadObjects[selectedIndex].transform.position) * 1.5f;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, quadLayerMask);

        // 거리순으로 정렬 (가까운 순서대로)
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            for (int i = 0; i < quadObjects.Length; i++)
            {
                if (quadObjects[i] == hit.collider.gameObject)
                {
                    Debug.Log($"Quad 감지: {hit.collider.name} (인덱스: {i})");

                    if (i == selectedIndex)
                    {
                        StartCoroutine(HighlightQuadObject(quadObjects[i]));
                        return quadObjects[i]; // 첫 번째 매칭에서 종료
                    }
                }
            }
        }

        return null;
    }

    // Quad 오브젝트 강조 효과 코루틴
    IEnumerator HighlightQuadObject(GameObject quad)
    {
        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color originalColor = renderer.material.color;
            renderer.material.color = Color.green;
            yield return new WaitForSeconds(1.5f);
            renderer.material.color = originalColor;
        }
    }

    void OnDestroy()
    {
        template?.Dispose();
        lastScreenMat?.Dispose();
        if (renderTexture != null) renderTexture.Release();
    }
}