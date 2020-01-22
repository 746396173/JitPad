﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace JitPad.Core.Processor
{
    public class JitMaker : IDisassembler
    {
        private readonly string _sourceCode;
        private readonly bool _isReleaseBuild;

        public JitMaker(string sourceCode, bool isReleaseBuild)
        {
            _sourceCode = sourceCode;
            _isReleaseBuild = isReleaseBuild;
        }

        public DisassembleResult Run()
        {
            var assemblyLoadContext = new UnloadableAssemblyLoadContext();
            var sourceCodeTempPath = Path.GetTempFileName() + ".cs";

            try
            {
                const string assemblyName = "compiled.dll";

                // todo: PDB embedded source code
                File.WriteAllText(sourceCodeTempPath, _sourceCode, Encoding.UTF8);

                var compiler = new Compiler();

                var compileResult = compiler.Compile(assemblyName, _sourceCode, sourceCodeTempPath, _isReleaseBuild);
                if (compileResult.IsOk == false)
                    return new DisassembleResult(false, "", compileResult.Messages);

                var assembly = assemblyLoadContext.LoadFromStream(new MemoryStream(compileResult.AssembleImage));

                var args = new[]
                {
                    "-m", assemblyName + ", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                    "-p", Process.GetCurrentProcess().Id.ToString(),
                    "--diffable"
                };

                var output = new StringWriter();

                var retCode = JitDasm.Program.MainForJitPad(compileResult.AssembleImage, assembly, output, args);

                return retCode == 0
                    ? new DisassembleResult(true, output.ToString(), Array.Empty<string>())
                    : new DisassembleResult(false, "", output.ToString().Split("\n"));
            }
            finally
            {
                File.Delete(sourceCodeTempPath);

                assemblyLoadContext.Unload();
            }
        }
    }
}