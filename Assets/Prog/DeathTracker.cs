using UnityEngine;

public class DeathTracker : MonoBehaviour
{
    public static DeathTracker Instance;

    public int TotalDeaths { get; private set; }
    public int VoidDeaths { get; private set; }
    public int TrapDeaths { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    public void RecordDeath(string reason)
    {
        TotalDeaths++;
        if (reason == "Void") VoidDeaths++;
        else TrapDeaths++;
    }

    public void ResetCounts()
    {
        TotalDeaths = 0;
        VoidDeaths = 0;
        TrapDeaths = 0;
    }
}
