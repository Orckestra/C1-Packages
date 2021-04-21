using System;
using System.IO;
using System.Web;
using Composite.Core.IO;
using LibSass.Compiler;
using LibSass.Compiler.Options;
using Orckestra.Web.Css.CompileFoundation;

namespace Orckestra.Web.Css.Sass
{
    public class SassCssCompiler : ICssCompiler
    {
        private static object _lockerCompilation = new object();
        private const string  FileExtention = ".scss";
        public bool SupportsExtension(string extension)
        {
            return string.Compare(extension, FileExtention, StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="sourceFilePath">virtual path to the Sass file</param>
        /// <returns>virtual path to the Css file</returns>
        public string CompileCss(string sourceFilePath)
        {
            var absoluteSassFilePath = HttpContext.Current.Server.MapPath(sourceFilePath);
            var filePathCss = absoluteSassFilePath.Substring(0, absoluteSassFilePath.Length - FileExtention.Length) + ".min.css";

            CompileCss(absoluteSassFilePath, filePathCss);

            return sourceFilePath.Substring(0, sourceFilePath.Length - FileExtention.Length) + ".min.css";
        }

        public static void CompileCss(string sassFilePath, string cssFilePath, DateTime? folderLastUpdatedUtc = null)
        {
            var sassCompiler = new SassCompiler(new SassOptions
            {
                InputPath = sassFilePath,
                OutputStyle = SassOutputStyle.Compact,
                IncludeSourceComments = false,
                IncludeSourceMapContents = true
            });


            var result = sassCompiler.Compile();


            if (result.ErrorStatus != 0)
            {
                throw new CssCompileException("Compiling sass caused a scripting host error. " +
                    string.Format("Error status: {0}. File: {1}. Line: {2}. Column: {3}. Message: {4}", result.ErrorStatus, result.ErrorFile, result.ErrorLine, result.ErrorColumn, result.ErrorMessage));
            }
            lock(_lockerCompilation)
            {
                C1File.WriteAllText(cssFilePath, result.Output);

                if (folderLastUpdatedUtc.HasValue)
                {
                    File.SetLastWriteTimeUtc(cssFilePath, folderLastUpdatedUtc.Value);
                }
            }
        }
    }
}
