using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{
    // All the mutable data a dialogue leaves behind: how many times each node was visited, which way
    // sticky rolls went, which one-shots were spent, and any "local." variables. The DialogueManager
    // always has a global instance of this; a conversation can be started with its own instance
    // instead (a POI passing one per point of interest, say), which is what makes "one shot" and
    // "already rolled" mean *here* instead of *ever*.
    //
    // Everything is keyed by strings built from the dialogue key plus a site suffix, so two different
    // rolls in the same node can't collide:
    //   "Key"             visit count of the node
    //   "Key#opt:<text>"  times an option was picked (only recorded for one-shot options)
    //   "Key#chance:<text>" outcome of an option's "{50%}*" roll
    //   "Key#group<n>"    the key chosen by the node's n-th weighted "{25%}=>" group
    //   "Key#code<n>"     times the node's n-th entry code block ran (only one-shot blocks)
    //   "Key#code"        times any of the node's entry code ran (what HasRun() reads)
    // The option text is used rather than its index because it survives reordering and reads better
    // in a save file. Note the keys do NOT include the file name - node keys are expected to be
    // namespaced ("AmmoDump:...") and the Update References validator warns about duplicates.
    //
    // Rolls store the *outcome*, not the rolled number: a save that says "chose AmmoDump:Search:Survivor"
    // stays on that branch even if the weights are edited later, and it can be read by a human.
    [Serializable]
    public class DialogueState
    {
        private Dictionary<string, int>     counts = new();
        private Dictionary<string, string>  groupRolls = new();
        private Dictionary<string, bool>    chanceRolls = new();
        private Dictionary<string, object>  variables = new();

        // ---------------------------------------------------------------------------------------
        // Counts (visits, option picks, code runs)
        // ---------------------------------------------------------------------------------------

        public int GetCount(string key)
        {
            return counts.TryGetValue(key, out var count) ? count : 0;
        }

        public void IncrementCount(string key)
        {
            counts[key] = GetCount(key) + 1;
        }

        // ---------------------------------------------------------------------------------------
        // Sticky rolls - the outcome is decided once and then just read back
        // ---------------------------------------------------------------------------------------

        public bool TryGetGroupRoll(string key, out string chosenKey)
        {
            return groupRolls.TryGetValue(key, out chosenKey);
        }

        public void SetGroupRoll(string key, string chosenKey)
        {
            groupRolls[key] = chosenKey;
        }

        // A stored branch that no longer exists (the file was edited) has to be rolled again
        public void ClearGroupRoll(string key)
        {
            groupRolls.Remove(key);
        }

        public bool TryGetChanceRoll(string key, out bool passed)
        {
            return chanceRolls.TryGetValue(key, out passed);
        }

        public void SetChanceRoll(string key, bool passed)
        {
            chanceRolls[key] = passed;
        }

        // ---------------------------------------------------------------------------------------
        // Variables ("local.<name>" in expressions ends up here, without the prefix)
        // ---------------------------------------------------------------------------------------

        public bool GetVarBool(string varName)
        {
            return (variables.TryGetValue(varName, out object value) && (value is bool boolValue)) && boolValue;
        }

        public float GetVarNumber(string varName)
        {
            return (variables.TryGetValue(varName, out object value) && (value is float floatValue)) ? floatValue : 0.0f;
        }

        public string GetVarString(string varName)
        {
            return (variables.TryGetValue(varName, out object value) && (value is string stringValue)) ? stringValue : "";
        }

        public Expression.DataType GetVariableDataType(string varName)
        {
            if (variables.TryGetValue(varName, out object value))
            {
                if (value is float) return Expression.DataType.Number;
                if (value is bool) return Expression.DataType.Bool;
                if (value is string) return Expression.DataType.String;
            }
            return Expression.DataType.Undefined;
        }

        public void SetVariable(string varName, float value) => variables[varName] = value;
        public void SetVariable(string varName, bool value) => variables[varName] = value;
        public void SetVariable(string varName, string value) => variables[varName] = value;

        public void Clear()
        {
            counts.Clear();
            groupRolls.Clear();
            chanceRolls.Clear();
            variables.Clear();
        }

        // ---------------------------------------------------------------------------------------
        // Serialization - JsonUtility can't do dictionaries, so everything goes through entry lists
        // ---------------------------------------------------------------------------------------

        [Serializable]
        class CountEntry { public string key; public int count; }
        [Serializable]
        class GroupEntry { public string key; public string chosen; }
        [Serializable]
        class ChanceEntry { public string key; public bool passed; }
        [Serializable]
        class VarEntry { public string name; public string type; public float number; public bool boolean; public string text; }

        [Serializable]
        class SaveData
        {
            public List<CountEntry> counts = new();
            public List<GroupEntry> groupRolls = new();
            public List<ChanceEntry> chanceRolls = new();
            public List<VarEntry> variables = new();
        }

        public string SerializeThis()
        {
            var data = new SaveData();
            foreach (var (key, count) in counts) data.counts.Add(new CountEntry { key = key, count = count });
            foreach (var (key, chosen) in groupRolls) data.groupRolls.Add(new GroupEntry { key = key, chosen = chosen });
            foreach (var (key, passed) in chanceRolls) data.chanceRolls.Add(new ChanceEntry { key = key, passed = passed });
            foreach (var (name, value) in variables)
            {
                var entry = new VarEntry { name = name };
                switch (value)
                {
                    case float f: entry.type = "number"; entry.number = f; break;
                    case bool b: entry.type = "bool"; entry.boolean = b; break;
                    case string s: entry.type = "string"; entry.text = s; break;
                    default: continue;
                }
                data.variables.Add(entry);
            }

            return JsonUtility.ToJson(data);
        }

        public void DeserializeThis(string json)
        {
            Clear();

            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            foreach (var entry in data.counts) counts[entry.key] = entry.count;
            foreach (var entry in data.groupRolls) groupRolls[entry.key] = entry.chosen;
            foreach (var entry in data.chanceRolls) chanceRolls[entry.key] = entry.passed;
            foreach (var entry in data.variables)
            {
                switch (entry.type)
                {
                    case "number": variables[entry.name] = entry.number; break;
                    case "bool": variables[entry.name] = entry.boolean; break;
                    case "string": variables[entry.name] = entry.text; break;
                }
            }
        }
    }
}
