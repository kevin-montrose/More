using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task takes all @reset() or @reset(selector) properties
    /// and goes off to find any matches, copying those properties as if
    /// they were selector includes.
    /// 
    /// However, @reset() will *only* copy those properties defined on
    /// classes that are in @reset{} blocks, copy on the sub-block level, and
    /// don't override *any* rules of the same name.
    /// 
    /// Copying on the sub-block level means that given:
    /// @reset{ div { a:b; } }
    /// 
    /// that 
    /// 
    /// .class { c:d; div { @reset(); e:f; } }
    /// 
    /// evaluates to
    /// 
    /// .class { c:d; } .class div { a:b; e:f;}
    /// 
    /// Naturally, @reset(selector) will match on the actual select passed.
    /// </summary>
    public class ResetIncludes
    {
        public static List<Block> Task(List<Block> blocks)
        {
            return blocks;
        }
    }
}
