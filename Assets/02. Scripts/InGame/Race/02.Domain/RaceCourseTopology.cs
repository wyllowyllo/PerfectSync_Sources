using System.Collections.Generic;

public static class RaceCourseTopology
{
    public static int ComputeLastCheckpointIndex(IReadOnlyList<(int from, int to)> edges)
    {
        if (edges == null || edges.Count == 0)
            return -1;

        int max = -1;
        for (int i = 0; i < edges.Count; i++)
        {
            int to = edges[i].to;
            if (to > max)
                max = to;
        }

        return max;
    }
}
