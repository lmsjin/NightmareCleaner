using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 솔로 테스트 허수아비(NCPlayer.IsDummy)의 입력을 만든다. Host에서만 호출된다.
/// 같은 Tick에 이동(NCPlayer)과 사격(NCGun)이 한 번씩 부르므로 Random 없이 Tick 번호로만 계산한다.
/// 행동은 NCBalance.soloDummy: 가만히 / 점령지 왕복 / 점령지 왕복 + 사격.
/// </summary>
public static class NCDummyBrain
{
    private const float ArriveDistance = 0.3f;

    public static NCInput Compute(NCPlayer dummy)
    {
        NCBalance balance = NCBalance.Get;
        NCInput input = new NCInput();
        Vector3 position = dummy.transform.position;

        NCPlayer target = FindTarget(dummy);
        input.AimPoint = Aim(dummy, target, balance);

        NCSession session = NCSession.Instance;
        if (balance.soloDummy == NCDummyMode.Stand || session == null)
        {
            return input;
        }

        input.Move = MoveToward(position, ZoneGoal(dummy, session, balance));

        // 부활·초기화 뒤에는 버튼을 한 번 떼야 다시 쏠 수 있으므로(NCGun.NeedRelease) 8 Tick마다 한 번 뗀다.
        bool release = (int)dummy.Runner.Tick % 8 == 0;
        if (balance.soloDummy == NCDummyMode.ZoneShoot && release == false && CanShoot(dummy, target, balance))
        {
            input.Buttons.Set(NCButton.Fire, true);
        }

        return input;
    }

    private static NCPlayer FindTarget(NCPlayer dummy)
    {
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player != dummy && player.Object != null && player.Object.IsValid && player.IsDummy == false)
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>점령지 밖이면 중심, 안이면 중심 좌우 지점을 번갈아 목표로 삼는다.</summary>
    private static Vector3 ZoneGoal(NCPlayer dummy, NCSession session, NCBalance balance)
    {
        Vector3 center = session.ZonePosition;
        float radius = balance.captureRadius;

        if (NCMath.FlatDistance(dummy.transform.position, center) > radius * 0.8f)
        {
            return center;
        }

        float seconds = (int)dummy.Runner.Tick * dummy.Runner.DeltaTime;
        bool right = Mathf.FloorToInt(seconds / Mathf.Max(0.1f, balance.dummyStrafeSeconds)) % 2 == 0;
        return center + new Vector3(right ? radius * 0.5f : -radius * 0.5f, 0f, 0f);
    }

    /// <summary>NavMesh 경로의 다음 꺾임점 쪽으로 걷는다(벽·가구를 돌아간다). 경로가 없으면 곧장.</summary>
    private static Vector2 MoveToward(Vector3 from, Vector3 goal)
    {
        Vector3 next = goal;
        NavMeshPath path = new NavMeshPath();

        if (NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas)
            && NavMesh.SamplePosition(goal, out NavMeshHit end, 1.5f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            && path.corners.Length > 1)
        {
            next = path.corners[1];
        }

        Vector3 to = NCMath.Flat(next - from);
        if (to.magnitude < ArriveDistance)
        {
            return Vector2.zero;
        }

        to.Normalize();
        return new Vector2(to.x, to.z);
    }

    /// <summary>상대 몸통 + Tick마다 흔들리는 오차. 상대가 없으면 앞쪽.</summary>
    private static Vector3 Aim(NCPlayer dummy, NCPlayer target, NCBalance balance)
    {
        Vector3 position = dummy.transform.position;

        if (target == null || target.Health.IsDown)
        {
            return position + dummy.transform.forward * 3f + Vector3.up;
        }

        float t = (int)dummy.Runner.Tick;
        float jitter = balance.dummyAimJitter;
        Vector3 offset = new Vector3(Mathf.Sin(t * 0.37f), 0f, Mathf.Cos(t * 0.23f)) * jitter;
        return target.transform.position + Vector3.up + offset;
    }

    private static bool CanShoot(NCPlayer dummy, NCPlayer target, NCBalance balance)
    {
        if (target == null || target.Health.IsDown)
        {
            return false;
        }

        Vector3 from = dummy.transform.position;
        Vector3 to = target.transform.position;
        return NCMath.FlatDistance(from, to) <= balance.shotRange && NCBulletSystem.HasSight(from, to);
    }
}
