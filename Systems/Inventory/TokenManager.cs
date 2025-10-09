using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class TokenManager : MonoBehaviour, IEnumerable<(Hypertag token, int count)>
    {
        public delegate void OnChange(bool add, Hypertag token, int quantity);
        public event OnChange onChange;

        private Dictionary<Hypertag, int> tokens;

        public void Add(Hypertag token, int quantity)
        {
            if (tokens == null) tokens = new();

            if (tokens.ContainsKey(token))
            {
                tokens[token]+= quantity;
            }
            else
            {
                tokens[token] = quantity;
            }

            onChange?.Invoke(true, token, quantity);
        }

        public void Add(Hypertag token)
        {
            if (tokens == null) tokens = new();

            if (tokens.ContainsKey(token))
            {
                tokens[token] += 1;
            }
            else
            {
                tokens[token] = 1;
            }

            onChange?.Invoke(true, token, 1);
        }

        public void Remove(Hypertag token, int count)
        {
            if (tokens == null) return;

            if (tokens.ContainsKey(token))
            {
                int toRemove = Mathf.Min(tokens[token], count);

                if (toRemove > 0)
                {                    
                    tokens[token] = Mathf.Max(0, tokens[token] - toRemove);

                    onChange?.Invoke(false, token, toRemove);
                }
            }
        }

        public void Remove(Hypertag token)
        {
            if (tokens == null) return;

            if (tokens.ContainsKey(token))
            {
                tokens[token] = Mathf.Max(0, tokens[token] - 1);

                onChange?.Invoke(false, token, 1);
            }
        }

        public bool HasToken(Hypertag token)
        {
            if (tokens == null) return false;

            if (tokens.TryGetValue(token, out var result))
            {
                return result > 0;
            }

            return false;
        }

        public bool HasToken()
        {
            if (tokens == null) return false;

            foreach (var i in tokens)
            {
                if (i.Value > 0) return true;
            }

            return false;
        }

        public int GetTokenCount(Hypertag token)
        {
            if (tokens == null) return 0;

            if (tokens.TryGetValue(token, out var result))
            {
                return result;
            }

            return 0;
        }

        public IEnumerator<(Hypertag token, int count)> GetEnumerator()
        {
            if (tokens != null)
            {
                foreach (var t in tokens)
                {
                    if (t.Value > 0)
                    {
                        yield return (t.Key, t.Value);
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}