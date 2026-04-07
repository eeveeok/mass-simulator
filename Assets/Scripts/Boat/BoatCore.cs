using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// 보트의 기본 구성 요소를 관리하는 핵심 컴포넌트
/// Rigidbody, AudioSource 및 필수 참조 설정
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class BoatCore : MonoBehaviour
{
    [Header("필수 참조")]
    public Transform boatModel;      // 보트 시각적 모델
    public WaterSurface water;       // HDRP 물 표면

    [Header("컴포넌트 참조")]
    public Transform[] buoyancyPoints; // 부력 적용 지점
    public Transform leftMotor;        // 왼쪽 모터 위치
    public Transform rightMotor;       // 오른쪽 모터 위치

    [HideInInspector] public Rigidbody RigidBody { get; private set; }
    [HideInInspector] public AudioSource CrashAudio { get; private set; }

    [System.NonSerialized] public int colNumInFinding;

    void Awake()
    {
        // 필수 컴포넌트 초기화
        RigidBody = GetComponent<Rigidbody>();
        CrashAudio = GetComponent<AudioSource>();

        // 물리 설정
        RigidBody.mass = 8f;
        RigidBody.linearDamping = 1.5f;   // 선형 감쇠
        RigidBody.angularDamping = 2f;    // 회전 감쇠
        RigidBody.interpolation = RigidbodyInterpolation.Interpolate; // 부드러운 물리
    }

    void Start()
    {
        // 부력점 자동 생성
        if (buoyancyPoints == null || buoyancyPoints.Length == 0)
            buoyancyPoints = CreateBuoyancyPoints();

        // 모터 위치 자동 생성
        if (leftMotor == null || rightMotor == null)
            CreateDefaultMotors();
    }

    /// <summary>
    /// 기본 부력점 생성 (5개 위치)
    /// </summary>
    Transform[] CreateBuoyancyPoints()
    {
        Vector3[] positions = {
            Vector3.zero,               // 중심
            new Vector3(0, 0, 0.2f),    // 앞쪽
            new Vector3(0, 0, -0.2f),   // 뒤쪽
            new Vector3(-0.1f, 0, 0),   // 왼쪽
            new Vector3(0.1f, 0, 0)     // 오른쪽
        };

        Transform[] points = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject point = new GameObject($"BuoyancyPoint_{i}");
            point.transform.SetParent(transform);
            point.transform.localPosition = positions[i];
            points[i] = point.transform;
        }
        return points;
    }

    /// <summary>
    /// 기본 모터 위치 생성
    /// </summary>
    void CreateDefaultMotors()
    {
        leftMotor = CreateMotor("LeftMotor", new Vector3(-0.1f, 0, 0));
        rightMotor = CreateMotor("RightMotor", new Vector3(0.1f, 0, 0));
    }

    /// <summary>
    /// 모터 게임오브젝트 생성
    /// </summary>
    Transform CreateMotor(string name, Vector3 localPos)
    {
        GameObject motor = new GameObject(name);
        motor.transform.SetParent(transform);
        motor.transform.localPosition = localPos;
        return motor.transform;
    }



    /// <summary>
    /// 충돌 시 이펙트 재생 (소리 + 파티클)
    /// </summary>
    private void PlayCrashEffect(Vector3 position)
    {
        if (CrashAudio != null)
            CrashAudio.Play();

        ParticleSystem particle = CreateDynamicParticle(position);

        if (particle != null)
            Destroy(particle.gameObject, particle.main.duration - 2f);
    }

    /// <summary>
    /// 현실적인 물보라 충돌 효과
    /// </summary>
    private ParticleSystem CreateDynamicParticle(Vector3 position)
    {
        GameObject particleObj = new GameObject("WaterSplash");
        particleObj.transform.position = position;
        
        ParticleSystem particle = particleObj.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particle.main;
        ParticleSystem.EmissionModule emission = particle.emission;
        ParticleSystem.ShapeModule shape = particle.shape;
        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();

        // 메인 설정
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.4f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
        main.startColor = new Color(1f, 0.95f, 0.1f, 0.9f);

        // 방출 설정
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 12) // 입자 수 감소
        });

        // 모양 설정 (반구형)
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.2f;
        shape.radiusThickness = 0.5f;

        // 렌더러 설정
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit")) {
            color = new Color(1f, 0.95f, 0.2f, 0.7f)
        };

        // 속도 변화 설정
        var velocityOverLifetime = particle.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;

        // 모든 방향으로 속도 적용
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-3f, 2f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f, -3f); // 위로 더 많이
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-3f, 2f);

        // 크기 변화 설정
        var sizeOverLifetime = particle.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f, 0f)
        ));

        // 회전 설정
        var rotationOverLifetime = particle.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

        // 파티클 즉시 재생
        particle.Play();

        return particle;
    }

    /// <summary>
    /// 트리거 충돌 감지 (장애물 태그)
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            colNumInFinding++;
            Debug.LogWarning($"{other.gameObject.name}와 충돌");

            // 첫 번째 충돌 지점 사용
            Vector3 collisionPoint = other.ClosestPoint(transform.position);
            // 충돌 사운드 재생
        if (CrashAudio != null)
            CrashAudio.Play();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        Collider other = collision.collider;

        if (other.CompareTag("Obstacle"))
        {
            colNumInFinding++;
            Debug.LogWarning($"{other.gameObject.name}와 충돌");

            // 첫 번째 충돌 지점 사용
            Vector3 collisionPoint = collision.contacts[0].point;
            PlayCrashEffect(collisionPoint);
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle
        {
            fontSize = 20,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(10, 10, 500, 30), $"속력: {RigidBody.linearVelocity.magnitude}", style);
    }
}