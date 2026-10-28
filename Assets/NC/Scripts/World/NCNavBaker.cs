using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// 시작할 때 맵의 NavMesh를 굽는다(작은 맵이라 순식간). 에디터에서 Bake 결과를 저장·관리할 필요가 없어진다.
/// 맵 루트(NavMeshSurface가 있는 오브젝트)에 붙인다. 유령 길찾기는 Host만 쓰지만, 모든 PC에서 구워도 문제없다.
/// 봉쇄 장벽은 꺼진 상태로 구워지고, 켜지면 NavMeshObstacle이 길을 막는다.
/// </summary>
[RequireComponent(typeof(NavMeshSurface))]
public class NCNavBaker : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<NavMeshSurface>().BuildNavMesh();

        // 플레이어·유령이 생기기 전에 미니맵용 맵 윤곽을 한 번 찍어 둔다.
        NCMapSnapshot.Capture(transform);
    }
}
