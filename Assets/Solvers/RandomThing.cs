using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RandomThing : MonoBehaviour
{
    List<int>[] diceValues;
    int diceCount;
    int resultingValue;
    // Start is called before the first frame update
    void Start()
    {
        List<int> allValues = diceValues[0];
        for (int i = 1; i < diceCount; i++) {
            var set = diceValues[i];
            var newSums = from x in allValues
                          from y in set
                          select (x + y);
            allValues = newSums.ToList();
        }
        var valuesWithCount = allValues.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Debug.Log(valuesWithCount[resultingValue]);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
