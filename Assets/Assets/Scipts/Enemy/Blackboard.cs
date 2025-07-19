using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blackboard : MonoBehaviour
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    //Adding new data
    //Template usage here
    public void Set<T>(string key, T value)
    {
        data[key] = value;
    }

    //Getting value
    public T Get<T>(string key)
    {
        if (data.ContainsKey(key) && data[key] is T)
            return (T)data[key];
        return default(T);
    }

    //Check if it has the data
    public bool Has(string key) { return data.ContainsKey(key); }
    
    //For removing key
    public void Remove(string key) {  data.Remove(key); }
}


/*
 Guide on how to use the blackboard:
For setting data:
- blackboard.Set("WhateverData", value);
For getting data:
- blackboard.Get<type>("WhateverData")
For checking existance of data
- blackboard.Has("WhateverData")
For removing existance of data
- blackboard.Remove("WhateverData")
 */