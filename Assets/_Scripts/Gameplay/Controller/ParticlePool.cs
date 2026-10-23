using System.Collections.Generic;
using UnityEngine;

public class ParticlePool : MonoBehaviour
{
    public static ParticlePool instance;
    public GameObject particlePrefab;
    private Queue<GameObject> pool = new Queue<GameObject>();
    private int maxCount;
    
    private void Awake()
    {
        instance = this;
    }
    
    public GameObject GetParticle(Vector3 position)
    {
        GameObject particle;
        if (pool.Count > 0)
        {
            particle = pool.Dequeue();
            particle.SetActive(true);
        }
        else
        {
            if (maxCount >= 13)
            {
                return null;
            }
            particle = Instantiate(particlePrefab);
            maxCount++;
        }
        particle.transform.position = position;
        return particle;
    }
    
    public void ReturnParticle(GameObject particle)
    {
        particle.SetActive(false);
        pool.Enqueue(particle);
    }
}
