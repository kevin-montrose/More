using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MoreInternals.Compiler;

namespace MoreInternals.Model
{
    class ReadOnlyDictionary<T,V>
    {
        private Dictionary<T, V> InnerDictionary;

        public V this[T key]
        {
            get
            {
                return InnerDictionary[key];
            }
        }

        public IEnumerable<T> Keys
        {
            get
            {
                return InnerDictionary.Keys;
            }
        }

        public ReadOnlyDictionary(Dictionary<T,V> dict)
        {
            InnerDictionary = dict;
        }

        public bool ContainsKey(T key)
        {
            return InnerDictionary.ContainsKey(key);
        }

        public bool TryGetValue(T key, out V value)
        {
            return InnerDictionary.TryGetValue(key, out value);
        }
    }

    class Scope
    {
        public const int MAX_DEPTH = 50;

        internal int Depth = 0;

        public Scope ParentScope { get; private set; }
        public ReadOnlyDictionary<string, Value> Variables { get; private set; }
        public ReadOnlyDictionary<string, MixinBlock> Mixins { get; private set; }
        public ReadOnlyDictionary<string, MoreFunction> Functions { get; private set; }
        public IPosition InvocationSite { get; private set; }

        private Scope(Scope parent, IPosition invocationSite, Dictionary<string, Value> variables, Dictionary<string, MixinBlock> mixins)
        {
            ParentScope = parent;
            InvocationSite = invocationSite;
            Depth = parent != null ? parent.Depth + 1 : 0;
            Variables = new ReadOnlyDictionary<string, Value>(variables);
            Mixins = new ReadOnlyDictionary<string, MixinBlock>(mixins);
            Functions = BuiltInFunctions.All;
        }

        public Scope(Dictionary<string, Value> variables, Dictionary<string, MixinBlock> mixins) : this(null, null, variables, mixins) { }

        public Scope Push(Dictionary<string, Value> variables, Dictionary<string, MixinBlock> mixins, IPosition invocationSite)
        {
            var ret =  new Scope(this, invocationSite, variables, mixins);

            if (ret.Depth > MAX_DEPTH)
            {
                var parent = this;
                while (parent.ParentScope != null && parent.ParentScope.InvocationSite != null)
                    parent = parent.ParentScope;

                Current.RecordError(ErrorType.Compiler, parent.InvocationSite, "Scope max depth exceeded, probably infinite recursion");
                throw new StoppedCompilingException();
            }

            return ret;
        }

        public Scope Pop()
        {
            if (ParentScope == null)
            {
                Current.RecordError(ErrorType.Compiler, Position.NoSite, "Cannot 'pop' the global scope");
                throw new StoppedCompilingException();
            }

            return ParentScope;
        }

        public Value LookupVariable(string name, int start, int stop, string filePath)
        {
            Value ret;
            if (Variables.TryGetValue(name, out ret)) return ret;

            if (ParentScope != null) return ParentScope.LookupVariable(name, start, stop, filePath);

            return NotFoundValue.Default.BindToPosition(start, stop, filePath);
        }

        public MixinBlock LookupMixin(string name, int lookupDepth = 0)
        {
            MixinBlock ret;
            if (Mixins.TryGetValue(name, out ret)) return ret;

            if (lookupDepth > 100)
            {
                Current.RecordError(ErrorType.Compiler, InvocationSite, "Scope max depth exceeded, probably infinite recursion");
                throw new StoppedCompilingException();
            }

            Value var;
            if (Variables.TryGetValue(name, out var))
            {
                var func = var as FuncValue;
                if(func != null)
                {
                    return LookupMixin(func.Name, lookupDepth + 1);
                }
            }

            if (ParentScope != null) return ParentScope.LookupMixin(name, lookupDepth + 1);

            return null;
        }

        public MoreFunction LookupFunction(string name)
        {
            // Written with the option of allowing user defined functions later,
            //   only built-ins for now though.

            MoreFunction ret;
            if (Functions.TryGetValue(name, out ret)) return ret;

            return null;
        }
    }
}
