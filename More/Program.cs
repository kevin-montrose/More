using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using More.Model;
using NDesk.Options;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using More.Helpers;
using More.Parser;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace More
{
    class Program
    {
        internal static bool Compile(string currentDir, string inputFile, TextWriter output, out List<string> spriteFiles)
        {
            Current.SetWorkingDirectory(currentDir);
            inputFile = inputFile.RebaseFile();

            using (var stream = File.OpenRead(inputFile))
            using (var @in = new StreamReader(stream))
            {
                return Compiler.Get().Compile(currentDir, inputFile, @in, output, FileLookup.Singleton, out spriteFiles);
            }
        }

        private static List<string> FindFiles(string inDirectory, string matchingPattern, bool recurse)
        {
            var ret = new List<string>();

            foreach (var file in Directory.EnumerateFiles(inDirectory, matchingPattern))
            {
                ret.Add(file);
            }

            if (recurse)
            {
                foreach (var dir in Directory.EnumerateDirectories(inDirectory))
                {
                    ret.AddRange(FindFiles(dir, matchingPattern, true));
                }
            }

            return ret;
        }

        private static string OutputFileFor(string file, bool overwrite)
        {
            var trial = Path.ChangeExtension(file, "css");
            int i = 1;

            while (!overwrite && File.Exists(trial))
            {
                trial = Path.GetDirectoryName(file) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) + " (" + i + ").css";
                i++;
            }

            return trial;
        }

        private static string PositionIn(string text, int pos)
        {
            var line = 1;
            var column = 1;

            for (int i = 0; i < text.Length && i < pos; i++)
            {
                column++;

                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
            }

            return string.Format("Line: {0}, Column: {1}", line, column);
        }

        private static void PrintErrors(List<Error> parseErrors = null, List<Error> compileErrors = null)
        {
            parseErrors = parseErrors ?? Current.GetErrors(ErrorType.Parser);
            if (parseErrors.Count > 0)
            {
                Console.WriteLine("Parse Errors");
                Console.WriteLine("============");
                foreach (var fileErrors in parseErrors.GroupBy(e => e.File))
                {
                    Console.WriteLine(fileErrors.Key);

                    string text;
                    if (File.Exists(fileErrors.Key))
                    {
                        text = File.ReadAllText(fileErrors.Key);
                    }
                    else
                    {
                        text = "";
                    }
                    foreach (var error in fileErrors.OrderBy(i => i.StartPosition).ThenBy(i => i.EndPosition))
                    {
                        string snippet;
                        using(var reader = new StringReader(text))
                        {
                            snippet = error.Snippet(reader);
                        }

                        if (error.StartPosition != error.EndPosition)
                        {
                            Console.WriteLine("between " + PositionIn(text, error.StartPosition) + " and " + PositionIn(text, error.EndPosition));
                        }
                        else
                        {
                            Console.WriteLine("near " + PositionIn(text, error.StartPosition));
                        }

                        Console.WriteLine(snippet.Trim());
                        Console.WriteLine(error.Message);
                    }

                    Console.WriteLine();
                }
            }

            compileErrors = compileErrors ?? Current.GetErrors(ErrorType.Compiler);
            if (compileErrors.Count > 0)
            {
                Console.WriteLine("Compilation Errors");
                Console.WriteLine("==================");
                foreach (var fileErrors in compileErrors.GroupBy(e => e.File))
                {
                    Console.WriteLine(fileErrors.Key);

                    string text;
                    if (File.Exists(fileErrors.Key))
                    {
                        text = File.ReadAllText(fileErrors.Key);
                    }
                    else
                    {
                        text = "";
                    }
                    foreach (var error in fileErrors.OrderBy(i => i.StartPosition).ThenBy(i => i.EndPosition))
                    {
                        string snippet;
                        using (var reader = new StringReader(text))
                        {
                            snippet = error.Snippet(reader);
                        }

                        if (error.StartPosition != error.EndPosition)
                        {
                            Console.WriteLine("between " + PositionIn(text, error.StartPosition) + " and " + PositionIn(text, error.EndPosition));
                        }
                        else
                        {
                            Console.WriteLine("near " + PositionIn(text, error.StartPosition));
                        }

                        Console.WriteLine(snippet.Trim());
                        Console.WriteLine(error.Message);
                    }

                    Console.WriteLine();
                }
            }
        }

        private static void PrintWarnings(List<Error> parseWarn = null, List<Error> compileWarn = null)
        {
            parseWarn = parseWarn ?? Current.GetWarnings(ErrorType.Parser);
            if (parseWarn.Count > 0)
            {
                Console.WriteLine("Parse Warnings");
                Console.WriteLine("==============");
                foreach (var fileErrors in parseWarn.GroupBy(e => e.File))
                {
                    Console.WriteLine(fileErrors.Key);

                    string text;
                    if (File.Exists(fileErrors.Key))
                    {
                        text = File.ReadAllText(fileErrors.Key);
                    }
                    else
                    {
                        text = "";
                    }
                    foreach (var error in fileErrors.OrderBy(i => i.StartPosition).ThenBy(i => i.EndPosition))
                    {
                        string snippet;
                        using (var reader = new StringReader(text))
                        {
                            snippet = error.Snippet(reader);
                        }

                        if (error.StartPosition != error.EndPosition)
                        {
                            Console.WriteLine("between " + PositionIn(text, error.StartPosition) + " and " + PositionIn(text, error.EndPosition));
                        }
                        else
                        {
                            Console.WriteLine("near " + PositionIn(text, error.StartPosition));
                        }

                        Console.WriteLine(snippet.Trim());
                        Console.WriteLine(error.Message);
                    }

                    Console.WriteLine();
                }
            }

            compileWarn = compileWarn ?? Current.GetWarnings(ErrorType.Compiler);
            if (compileWarn.Count > 0)
            {
                Console.WriteLine("Compilation Warnings");
                Console.WriteLine("====================");
                foreach (var fileErrors in compileWarn.GroupBy(e => e.File))
                {
                    Console.WriteLine(fileErrors.Key);

                    string text;
                    if (File.Exists(fileErrors.Key))
                    {
                        text = File.ReadAllText(fileErrors.Key);
                    }
                    else
                    {
                        text = "";
                    }
                    foreach (var error in fileErrors.OrderBy(i => i.StartPosition).ThenBy(i => i.EndPosition))
                    {
                        string snippet;
                        using (var reader = new StringReader(text))
                        {
                            snippet = error.Snippet(reader);
                        }

                        if (error.StartPosition != error.EndPosition)
                        {
                            Console.WriteLine("between " + PositionIn(text, error.StartPosition) + " and " + PositionIn(text, error.EndPosition));
                        }
                        else
                        {
                            Console.WriteLine("near " + PositionIn(text, error.StartPosition));
                        }

                        Console.WriteLine(snippet.Trim());
                        Console.WriteLine(error.Message);
                    }

                    Console.WriteLine();
                }
            }
        }

        static void RunSpriteCommand(string command, string workingDir, string file, string spriteArguments, ConcurrentBag<Error> errors, ConcurrentBag<Error> warnings, ConcurrentBag<string> info)
        {
            try
            {
                string finalArgs;
                if (spriteArguments.HasValue()) finalArgs = spriteArguments; else finalArgs = "";

                finalArgs = finalArgs.Trim() + " \"" + file + "\"";

                var startInfo = new ProcessStartInfo(command, finalArgs);
                startInfo.WorkingDirectory = workingDir;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;

                var errorOutput = new StringBuilder();

                var proc = new Process();
                proc.StartInfo = startInfo;
                proc.ErrorDataReceived += 
                    delegate(object sendingProc, DataReceivedEventArgs args)
                    {
                        errorOutput.Append(args.Data);
                    };

                proc.Start();

                proc.BeginErrorReadLine();
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    errors.Add(OutOfProcError.Create(ErrorType.Compiler, "Error by [" + command + "] for [" + file + "] exited with code " + proc.ExitCode));
                }

                if (output.HasValue())
                {
                    info.Add("Written by [" + command + "] for [" + file + "]:\r\n" + output);
                }

                if (errorOutput.Length != 0)
                {
                    errors.Add(OutOfProcError.Create(ErrorType.Compiler, "Error by [" + command + "] for [" + file + "]:\r\n" + errorOutput.ToString()));
                }
            }
            catch (Exception e)
            {
                errors.Add(OutOfProcError.Create(ErrorType.Compiler, "Error encountered executing [" + command + "], \"" + e.Message + "\""));
            }
        }

        static void MultiThreadedCompile(int maxParallelism, string workingDirectory, List<string> toCompile, bool overwrite, bool warnAsErrors, bool minify, bool optimize, bool verbose, string spriteProg, string spriteArguments)
        {
            var @lock = new Semaphore(0, toCompile.Count);
            var outMsg = new ConcurrentBag<string>();
            var errors = new ConcurrentBag<Error>();
            var warnings = new ConcurrentBag<Error>();
            var infos = new ConcurrentBag<string>();
            var sprites = new ConcurrentBag<string>();

            toCompile.AsParallel()
                .WithDegreeOfParallelism(maxParallelism)
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .ForAll(
                    delegate(string compile)
                    {
                        try
                        {
                            Current.Reset();

                            if (minify)
                            {
                                Current.SetOptions(Options.Minify);
                                Current.SetWriterMode(WriterMode.Minimize);
                            }

                            if (optimize)
                            {
                                Current.SetOptions(Options.OptimizeCompression);
                            }

                            var buffer = new StringBuilder();

                            var outputFile = OutputFileFor(compile, overwrite: overwrite);
                            buffer.AppendLine("\t" + compile);
                            buffer.Append("\tto " + outputFile);
                            using (var newFile = File.OpenWrite(outputFile))
                            using (var output = new StreamWriter(newFile))
                            {
                                Current.SetOptions(warnAsErrors ? Options.WarningsAsErrors : Options.None);
                                var timer = new Stopwatch();
                                timer.Start();

                                List<string> generatedSprites;
                                var result = Compile(workingDirectory, compile, output, out generatedSprites);

                                timer.Stop();

                                if (result)
                                {
                                    buffer.AppendLine(" in " + timer.ElapsedMilliseconds + "ms");
                                }
                                else
                                {
                                    buffer.AppendLine(" failed after " + timer.ElapsedMilliseconds + "ms");
                                }

                                if (Current.HasErrors())
                                {
                                    foreach (var error in Current.GetAllErrors())
                                    {
                                        errors.Add(error);
                                    }
                                }

                                if (Current.HasWarnings())
                                {
                                    foreach (var warning in Current.GetAllWarnings())
                                    {
                                        warnings.Add(warning);
                                    }
                                }

                                foreach (var i in Current.GetInfo())
                                {
                                    infos.Add(i);
                                }

                                foreach (var sprite in generatedSprites)
                                {
                                    sprites.Add(sprite);
                                }

                                outMsg.Add(buffer.ToString());
                            }
                        }
                        finally
                        {
                            @lock.Release();
                        }
                    }
                );

            for (int i = 0; i < toCompile.Count; i++)
                @lock.WaitOne();

            if (spriteProg.HasValue())
            {
                foreach (var sprite in sprites)
                {
                    RunSpriteCommand(spriteProg, workingDirectory, sprite, spriteArguments, errors, warnAsErrors ? errors : warnings, infos);
                }
            }

            if (verbose)
            {
                foreach (var msg in outMsg)
                {
                    Console.Write(msg);
                }

                if (outMsg.Count > 0) Console.WriteLine();
            }

            if (errors.Count > 0)
            {
                var parseErrors = errors.Where(e => e.Type == ErrorType.Parser).Distinct().ToList();
                var compileErrors = errors.Where(e => e.Type == ErrorType.Compiler).Distinct().ToList();

                PrintErrors(parseErrors: parseErrors, compileErrors: compileErrors);
            }

            if (warnings.Count > 0)
            {
                var parseWarnings = warnings.Where(e => e.Type == ErrorType.Parser).Distinct().ToList();
                var compileWarnings = warnings.Where(e => e.Type == ErrorType.Compiler).Distinct().ToList();

                PrintWarnings(parseWarn: parseWarnings, compileWarn: compileWarnings);
            }
            if (verbose && infos.Count > 0)
            {
                Console.WriteLine("INFO");
                Console.WriteLine("====");
                foreach (var i in infos)
                {
                    Console.WriteLine(i);
                }
            }
        }

        static void Main(string[] args)
        {
            var filePattern = "";
            var workingDirectory = Environment.CurrentDirectory;
            var recurse = false;
            var showHelp = false;
            var overwrite = false;
            var warnAsErrors = false;
            var maxDegreeParallelism = Math.Max(1, Environment.ProcessorCount - 1);
            var minify = false;
            var optimize = false;
            var verbose = false;
            string spriteProg = null;
            string spriteArguments = null;

            var options = new OptionSet()
            {
                { "r", "Recursively search all folders for matching files to compile", r => recurse = r != null },
                { "f|force", "Force overwriting of existing css files.", f => overwrite = f != null },
                { "wd:", "Sets the working directory for compilation. ~ in paths will be resolved with the working directory where needed.", wd => workingDirectory = wd },
                { "<>", "File pattern to compile.", d => filePattern = d },
                { "wae|warnaserror", "Treat warnings as errors", w => warnAsErrors = w != null },
                { "?|help", "show this message and exit", h => showHelp = h != null },
                { "mt:|maxthreads:", "maximum number of threads to use during compilation", t => maxDegreeParallelism = Int32.Parse(t) },
                { "m|minify", "minify output", m => minify = m != null },
                { "o|optimize", "optimize for compression", o => optimize = o != null },
                { "v|verbose", "print info output", v => verbose = v != null },
                { "sp:|spriteprocessor:", "program to run on generated sprites, the sprite will be passed after spritearguments argument", sc => spriteProg = sc },
                { "sa:|spritearguments:", "arguments to pass to spriteprocessor before the sprite file", sa => spriteArguments = sa }
            };

            options.Parse(args);

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var toCompile = FindFiles(workingDirectory, filePattern, recurse);

            if (toCompile.Count == 0)
            {
                Console.WriteLine("No files found to compile");
                return;
            }

            if (verbose)
            {
                Console.WriteLine("Compiling (" + toCompile.Count + ") files...");
            }

            MultiThreadedCompile(maxDegreeParallelism, workingDirectory, toCompile, overwrite, warnAsErrors, minify, optimize, verbose, spriteProg, spriteArguments);

            if (verbose)
            {
                Console.ReadKey();
            }
        }
    }
}