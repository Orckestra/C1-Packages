﻿using System;
using System.CodeDom.Compiler;

namespace Orckestra.AspNet.Roslyn
{
    /// <exclude />
    public sealed class CSharpCodeProvider : Microsoft.CSharp.CSharpCodeProvider
    {

        //public CSharpCodeProvider() : this(null)
        //{
        //}

        //internal CSharpCodeProvider(ICompilerSettings compilerSettings = null)
        //{
        //}

        /// <exclude />
        [Obsolete("Callers should not use the ICodeCompiler interface and should instead use the methods directly on the CodeDomProvider class.")]
        public override ICodeCompiler CreateCompiler()
        {
            return new CSharpCompiler(this);
        }
    }
}
