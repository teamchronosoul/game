[System.Serializable]
public class VNMbtiState
{
    public int E;
    public int I;
    public int S;
    public int N;
    public int T;
    public int F;
    public int J;
    public int P;

    public string ResultType;      // например INTJ
    public string ArchetypeId;     // Logics / Diplomats / Defenders / Seekers
    public string ArchetypeName;   // Логики / Дипломаты / Защитники / Искатели
    public string ResultColorHex;  // например #4A8BFF

    public void Reset()
    {
        E = I = S = N = T = F = J = P = 0;
        ResultType = "";
        ArchetypeId = "";
        ArchetypeName = "";
        ResultColorHex = "";
    }
}