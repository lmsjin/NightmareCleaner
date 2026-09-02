using UnityEngine;

public class VacuumParticleAttractor : MonoBehaviour
{
    public ParticleSystem ps;
    public Transform vacuumMouth;

    [Header("흡입 설정")]
    public float attractSpeed = 8f;
    public float attractDelay = 1f;     // 생성 후 흡입 시작 시간
    public float rampUpTime = 1f;       // 흡입력이 최대가 되기까지 걸리는 시간
    public float destroyDistance = 0.15f;

    private ParticleSystem.Particle[] particles;

    void LateUpdate()
    {
        if (ps == null || vacuumMouth == null)
            return;

        int count = ps.particleCount;

        if (particles == null || particles.Length < count)
            particles = new ParticleSystem.Particle[count];

        ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            float age = particles[i].startLifetime - particles[i].remainingLifetime;

            // 생성 후 attractDelay초 동안은 흡입 안 함
            if (age < attractDelay)
                continue;

            // 점진적으로 흡입력 증가
            float attractWeight = Mathf.Clamp01(
                (age - attractDelay) / rampUpTime
            );

            Vector3 target = vacuumMouth.position;

            particles[i].position = Vector3.MoveTowards(
                particles[i].position,
                target,
                attractSpeed * attractWeight * Time.deltaTime
            );

            float dist = Vector3.Distance(
                particles[i].position,
                target
            );

            if (dist < destroyDistance)
            {
                particles[i].remainingLifetime = 0f;
            }
        }

        ps.SetParticles(particles, count);
    }
}