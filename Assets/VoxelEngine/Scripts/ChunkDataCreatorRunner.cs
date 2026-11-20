using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
public class ChunkDataCreatorRunner : MonoBehaviour
{
    public bool Run = false;
    public void OnValidate()
    {
        if (Run)
        {
            Run = false;
            GetComponentsInChildren<ChunkDataCreator>().ToList().ForEach(c => c.Run = true);
        }
    }
}
#endif