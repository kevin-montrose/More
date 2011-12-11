using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MoreInternals.Helpers;
using MoreInternals.Compiler;

namespace MoreInternals.Model
{
    // CssRules = CssRule [CssRules] | RuleInclude [CssRules] | CssClass [CssRules]
    class Property : IPosition
    {
        public int Start { get; protected set; }
        public int Stop { get; protected set; }
        public string FilePath { get; protected set; }
    }

    /* CssRule = NAME COLON CssValue SEMI_COLON */
    class NameValueProperty : Property, IWritable
    {
        public string Name { get; private set; }
        
        public Value Value { get; private set; }

        internal NameValueProperty(string name, Value value, int start = -1, int stop = -1, string filePath = null)
        {
            Name = name;
            Value = value;
            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public void Write(ICssWriter output)
        {
            output.WriteRule(this);
        }
    }

    class MixinApplicationParameter
    {
        public string Name { get; private set; }
        public Value Value { get; private set; }

        public MixinApplicationParameter(string name, Value value)
        {
            Name = name;
            Value = value;
        }
    }

    /* RULE NAME [MixinApplication] SEMI_COLON */
    class MixinApplicationProperty : Property
    {
        public string Name { get; private set; }

        public bool IsOptional { get; private set; }
        public bool DoesOverride { get; private set; }

        public IEnumerable<MixinApplicationParameter> Parameters { get; private set; }

        internal MixinApplicationProperty(string name, List<MixinApplicationParameter> parameters, bool optional, bool overrides, int start, int stop, string filePath)
        {
            Name = name;

            Parameters = parameters.AsReadOnly();

            IsOptional = optional;
            DoesOverride = overrides;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        internal List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            if (!IsOptional)
            {
                ret.Add(Name);
            }

            foreach (var p in Parameters)
            {
                ret.AddRange(p.Value.ReferredToVariables());
            }

            return ret;
        }
    }

    class IncludeSelectorProperty : Property
    {
        public Selector Selector { get; set; }

        public bool Overrides { get; set; }

        public IncludeSelectorProperty(Selector selector, bool overrides, int start, int stop, string file)
        {
            Selector = selector;

            Overrides = overrides;

            Start = start;
            Stop = stop;
            FilePath = file;
        }

        internal List<NameValueProperty> LookupMatch(List<SelectorAndBlock> unrolled, HashSet<int> callChain = null, List<SelectorAndBlock> parent = null)
        {
            // blocks in @reset declarations cannot be referred to via @() selector includes.
            unrolled = unrolled.Where(w => !w.IsReset).ToList();

            callChain = callChain ?? new HashSet<int>();

            var ret = new List<NameValueProperty>();

            var parts = new List<Selector>();
            var asMulti = Selector as MultiSelector;
            if (asMulti != null)
            {
                parts.AddRange(asMulti.Selectors);
            }
            else
            {
                parts.Add(Selector);
            }

            foreach (var s in parts)
            {
                foreach (var x in unrolled.Where(u => u.Selector.Equals(s)))
                {
                    if (callChain.Contains(x.Id))
                    {
                        Current.RecordError(ErrorType.Compiler, x, "Found circular reference in selector include, " + s + " to " + x);
                        throw new StoppedCompilingException();
                    }

                    ret.AddRange(x.Properties.OfType<NameValueProperty>());
                    ret.AddRange(
                        x.Properties.OfType<IncludeSelectorProperty>().SelectMany(
                            j => 
                            {
                                var chain = new HashSet<int>(callChain);
                                chain.Add(x.Id);
                                return j.LookupMatch(unrolled, chain, parent);
                            }
                        )
                    );
                }
            }

            // If there was no match in the current scope, try the parent one
            if (ret.Count == 0 && parent != null)
            {
                return LookupMatch(parent);
            }

            return ret;
        }
    }

    class NestedBlockProperty : Property
    {
        public SelectorAndBlock Block { get; private set; }

        internal NestedBlockProperty(SelectorAndBlock innerBlock, int start, int stop)
        {
            Block = innerBlock;

            Start = start;
            Stop = stop;
        }
    }

    class VariableProperty : Property
    {
        public string Name { get; private set; }
        public Value Value {get; private set;}

        internal VariableProperty(string name, Value value, int start, int stop, string file)
        {
            Name = name;
            Value = value;

            Start = start;
            Stop = stop;
            FilePath = file;
        }
    }
}
