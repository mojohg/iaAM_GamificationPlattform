using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ChartAndGraph;
public class GraphChartFeed : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GraphChart graph = GetComponent<GraphChart>();
        if(graph != null)
        {
            graph.DataSource.StartBatch();
            graph.DataSource.ClearCategory("Player 1");
            graph.DataSource.ClearCategory("Player 2");
            for(int i=0;i<30;i++)
            {
                graph.DataSource.AddPointToCategory("Player 1",Random.value*10f,Random.value*10f);
                graph.DataSource.AddPointToCategory("Player 2", Random.value * 10f,Random.value * 10f);
            }graph.DataSource.EndBatch();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
