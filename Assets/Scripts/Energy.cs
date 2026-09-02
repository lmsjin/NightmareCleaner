using UnityEngine;

public class EnergyShooter : MonoBehaviour
{
    public GameObject energyPrefab;
    public Transform firePoint;

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.F))
        {
            Instantiate(
                energyPrefab,
                firePoint.position,
                firePoint.rotation
            );
        }
    }
}