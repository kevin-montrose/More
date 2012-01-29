using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MoreInternals.Model;
using NDesk.Options;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using MoreInternals.Helpers;
using MoreInternals.Parser;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MoreInternals;
using MoreInternals.Compiler;

namespace More
{
    class Program
    {
        enum ExitCode : int
        {
            Success = 0,
            BadParameters = 1,
            CompilationErrors = 2,
            Crash = 3
        }

        private static FileCache FileCache = new FileCache();

        internal static bool Compile(string currentDir, string inputFile, Context context, bool minify, bool warnAsErrors)
        {
            var opts = Options.None;
            var writerMode = WriterMode.Pretty;

            if (minify)
            {
                opts |= Options.Minify;
                writerMode = WriterMode.Minimize;
            }

            if (warnAsErrors)
            {
                opts |= Options.WarningsAsErrors;
            }

            return Compiler.Get().Compile(currentDir, inputFile, FileLookup.Singleton, context, opts, writerMode);
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

        private static void Print(string errorClass, List<Error> parse = null, List<Error> compile = null)
        {
            if (parse.Count > 0)
            {
                Console.WriteLine("Parse " + errorClass);
                Console.WriteLine("============");
                foreach (var fileErrors in parse.GroupBy(e => e.File))
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

            if (compile.Count > 0)
            {
                Console.WriteLine("Compilation " + errorClass);
                Console.WriteLine("==================");
                foreach (var fileErrors in compile.GroupBy(e => e.File))
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

        static List<Error> RunSpriteCommand(string command, string workingDir, string file, string spriteArguments, List<string> infoMessages)
        {
            var ret = new Dictionary<ErrorType, List<Error>>();
            var errors = new List<Error>();

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
                    infoMessages.Add("Written by [" + command + "] for [" + file + "]:\r\n" + output);
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

            return errors;
        }

        static bool MultiThreadedCompile(int maxParallelism, string workingDirectory, List<string> toCompile, bool overwrite, bool warnAsErrors, bool minify, bool verbose, string spriteProg, string spriteArguments)
        {
            var @lock = new Semaphore(0, toCompile.Count);
            var contexts = new ConcurrentBag<Context>();
            var outMsg = new ConcurrentBag<string>();

            toCompile.AsParallel()
                .WithDegreeOfParallelism(maxParallelism)
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .ForAll(
                    delegate(string compile)
                    {
                        try
                        {
                            var threadContext = new Context(FileCache);
                            contexts.Add(threadContext);                            

                            var buffer = new StringBuilder();

                            var outputFile = OutputFileFor(compile, overwrite: overwrite);
                            buffer.AppendLine("\t" + compile);
                            buffer.Append("\tto " + outputFile);

                            var timer = new Stopwatch();
                            timer.Start();

                            var result = Compile(workingDirectory, compile, threadContext, minify, warnAsErrors);

                            timer.Stop();

                            if (result)
                            {
                                buffer.AppendLine(" in " + timer.ElapsedMilliseconds + "ms");
                            }
                            else
                            {
                                buffer.AppendLine(" failed after " + timer.ElapsedMilliseconds + "ms");
                            }

                            outMsg.Add(buffer.ToString());
                        }
                        finally
                        {
                            @lock.Release();
                        }
                    }
                );

            for (int i = 0; i < toCompile.Count; i++)
                @lock.WaitOne();

            var mergedContext = contexts.ElementAt(0);
            for (int i = 1; i < contexts.Count; i++)
            {
                mergedContext = mergedContext.Merge(contexts.ElementAt(i));
            }

            var infoMessages = mergedContext.GetInfoMessages().ToList();
            var errors = mergedContext.GetErrors();

            if (spriteProg.HasValue())
            {
                foreach (var sprite in mergedContext.GetSpriteFiles())
                {
                    var commandErrors = RunSpriteCommand(spriteProg, workingDirectory, sprite, spriteArguments, infoMessages);

                    errors = errors.SelectMany(s => s.ToList()).Union(commandErrors).ToLookup(k => k.Type);
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
                var parseErrors = errors.Where(e => e.Key == ErrorType.Parser).SelectMany(s => s.ToList()).Distinct().ToList();
                var compileErrors = errors.Where(e => e.Key == ErrorType.Compiler).SelectMany(s => s.ToList()).Distinct().ToList();

                Print("Errors", parseErrors, compileErrors);
            }

            if (mergedContext.GetWarnings().Count > 0)
            {
                var parseWarnings = mergedContext.GetWarnings().Where(e => e.Key == ErrorType.Parser).SelectMany(s => s.ToList()).Distinct().ToList();
                var compileWarnings = mergedContext.GetWarnings().Where(e => e.Key == ErrorType.Compiler).SelectMany(s => s.ToList()).Distinct().ToList();

                Print("Warnings", parseWarnings, compileWarnings);
            }

            if (verbose && infoMessages.Count > 0)
            {
                Console.WriteLine("INFO");
                Console.WriteLine("====");
                foreach (var i in infoMessages)
                {
                    Console.WriteLine(i);
                }
            }

            return mergedContext.GetErrors().Count == 0;
        }

        private static bool VerifyParameters(string workingDirectory, int maxDegreeParallelism, string spriteProg)
        {
            var ret = true;

            if (workingDirectory.HasValue() && !Directory.Exists(workingDirectory))
            {
                Console.WriteLine("Could not find directory [" + workingDirectory + "]");
                ret = false;
            }

            if (spriteProg.HasValue() && !File.Exists(spriteProg))
            {
                Console.WriteLine("Could not find program [" + spriteProg + "]");
                ret = false;
            }

            if (maxDegreeParallelism <= 0)
            {
                Console.WriteLine("maxthreads must be >= 0");
                return false;
            }

            return ret;
        }

        static int Main(string[] args)
        {
            try
            {
                var filePattern = "";
                var workingDirectory = Environment.CurrentDirectory;
                var recurse = false;
                var showHelp = false;
                var overwrite = false;
                var warnAsErrors = false;
                var maxDegreeParallelism = Math.Max(1, Environment.ProcessorCount - 1);
                var minify = false;
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
                    { "v|verbose", "print info output", v => verbose = v != null },
                    { "sp:|spriteprocessor:", "program to run on generated sprites, the sprite will be passed after spritearguments argument", sc => spriteProg = sc },
                    { "sa:|spritearguments:", "arguments to pass to spriteprocessor before the sprite file", sa => spriteArguments = sa }
                };

                options.Parse(args);

                if (showHelp)
                {
                    options.WriteOptionDescriptions(Console.Out);
                    return (int)ExitCode.Success;
                }

                if (!VerifyParameters(workingDirectory, maxDegreeParallelism, spriteProg))
                {
                    return (int)ExitCode.BadParameters;
                }

                var toCompile = FindFiles(workingDirectory, filePattern, recurse);

                if (toCompile.Count == 0)
                {
                    Console.WriteLine("No files found to compile");
                    return (int)ExitCode.BadParameters;
                }

                if (verbose)
                {
                    Console.WriteLine("Compiling (" + toCompile.Count + ") files...");
                }

                var success = MultiThreadedCompile(maxDegreeParallelism, workingDirectory, toCompile, overwrite, warnAsErrors, minify, verbose, spriteProg, spriteArguments);

                if (verbose)
                {
                    Console.ReadKey();
                }

                return (int)(success ? ExitCode.Success : ExitCode.CompilationErrors);
            }
            catch (Exception e)
            {
                var errorFile = "error-" + Guid.NewGuid() + ".log";

                Console.WriteLine();
                Console.WriteLine("!!!An Error Occurred!!!");
                Console.Write("Dumping to ["+errorFile+"]...");

                using (var file = File.OpenWrite(errorFile))
                using (var text = new StreamWriter(file))
                {
                    text.WriteLine("Operating System: " + Environment.OSVersion);
                    text.WriteLine("64 Bit? " + Environment.Is64BitProcess);
                    text.WriteLine("Path Separator: " + Path.DirectorySeparatorChar);
                    text.WriteLine("Command Line: " + Environment.CommandLine);
                    text.WriteLine("On Date: " + DateTime.UtcNow);
                    text.WriteLine();
                    text.WriteLine("Exception");
                    text.WriteLine("---------");
                    text.WriteLine(e.Message);
                    text.WriteLine(e.StackTrace);
                    text.WriteLine();

                    try
                    {
                        var compiler = Compiler.Get();

                        var header = "(" + FileCache.Count + ") files in compiler cache";

                        text.WriteLine(header);
                        for (int i = 0; i < header.Length; i++) text.Write('=');
                        text.WriteLine();

                        foreach (var path in FileCache.Loaded())
                        {
                            string more = null;

                            try
                            {
                                more = File.ReadAllText(path);
                            }
                            catch (Exception) { /* Indicative of IO trouble */ }

                            if (more.HasValue())
                            {
                                var pathHeader = "[" + path + "] of " + more.Length + " characters";
                                text.WriteLine(pathHeader);
                                for (int i = 0; i < pathHeader.Length; i++) text.Write('-');
                                text.WriteLine();
                                text.WriteLine(more);
                            }
                            else
                            {
                                text.WriteLine("**[" + path + "] could not read**");
                            }

                            text.WriteLine();
                        }
                    }
                    catch (Exception) { /* Abandon all hope, ye who enter here */ }
                }

                Console.WriteLine(" done!");
                Console.WriteLine();
                Console.WriteLine("Please submit this bug report");

                return (int)ExitCode.Crash;
            }
        }
    }
}